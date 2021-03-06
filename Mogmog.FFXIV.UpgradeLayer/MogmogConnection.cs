﻿using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Events;
using Mogmog.Logging;
using Mogmog.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Grpc.Core.Metadata;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnection : IDisposable
    {
        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private readonly CancellationTokenSource tokenSource;
        private readonly ChatServiceClient chatClient;
        private readonly DiscordOAuth2 oAuth2;
        private readonly GrpcChannel channel;

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public int ChannelId { get; set; }
        public bool SaveAccessCode { get; set; }

        public MogmogConnection(string hostname, int channelId, bool saveAccessCode)
        {
            this.ChannelId = channelId;
            this.SaveAccessCode = saveAccessCode;
            this.tokenSource = new CancellationTokenSource();
            this.channel = GrpcChannel.ForAddress(hostname);
            this.chatClient = new ChatServiceClient(channel);
            var serverInfo = this.chatClient.GetChatServerInfo(new ReqChatServerInfo());
            var flags = (ServerFlags)serverInfo.Flags;
            Mogger.Log($"Server flags for {hostname}: {flags}");
            var callOptions = new CallOptions()
                .WithCancellationToken(this.tokenSource.Token)
                .WithDeadline(DateTime.UtcNow.AddMinutes(1))
                .WithWaitForReady();
            if (flags.HasFlag(ServerFlags.RequiresDiscordOAuth2))
            {
                oAuth2 = new DiscordOAuth2();
                oAuth2.Authenticate(serverInfo.ServerId);
                var headers = new Metadata
                {
                    new Entry("code", oAuth2.OAuth2Code),
                };
                callOptions = callOptions.WithHeaders(headers);
            }
            this.chatStream = this.chatClient.Chat(callOptions);
            _ = ChatLoop(this.tokenSource.Token);
        }
        
        public void SendMessage(ChatMessage message)
        {
            this.chatStream.RequestStream.WriteAsync(message);
        }

        private async Task ChatLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await this.chatStream.ResponseStream.MoveNext(cancellationToken))
                    continue;
                MessageReceivedEvent(this, new MessageReceivedEventArgs {
                    Message = chatStream.ResponseStream.Current,
                    ChannelId = this.ChannelId + 1,
                });
            }
        }

        #region Moderation Commands
        public async Task BanUser(string name, int worldId, string thisUserName, int thisUserWorldId)
            => await this.chatClient.BanUserAsync(BuildActionRequest(name, worldId, thisUserName, thisUserWorldId));

        public async Task UnbanUser(string name, int worldId, string thisUserName, int thisUserWorldId)
            => await this.chatClient.UnbanUserAsync(BuildActionRequest(name, worldId, thisUserName, thisUserWorldId));

        public async Task TempbanUser(string name, int worldId, DateTime end, string thisUserName, int thisUserWorldId)
            => await this.chatClient.TempbanUserAsync(new TempbanUserRequest { UserName = name, UserWorldId = worldId, UnbanTimestamp = end.ToBinary(), OAuth2Code = oAuth2.OAuth2Code ?? string.Empty, ThisUserName = thisUserName, ThisUserWorldId = thisUserWorldId });

        public async Task KickUser(string name, int worldId, string thisUserName, int thisUserWorldId)
            => await this.chatClient.KickUserAsync(BuildActionRequest(name, worldId, thisUserName, thisUserWorldId));

        public async Task MuteUser(string name, int worldId, string thisUserName, int thisUserWorldId)
            => await this.chatClient.MuteUserAsync(BuildActionRequest(name, worldId, thisUserName, thisUserWorldId));

        public async Task UnmuteUser(string name, int worldId, string thisUserName, int thisUserWorldId)
            => await this.chatClient.UnmuteUserAsync(BuildActionRequest(name, worldId, thisUserName, thisUserWorldId));

        private UserActionRequest BuildActionRequest(string name, int worldId, string thisUserName, int thisUserWorldId)
        {
            return new UserActionRequest
            {
                UserName = name,
                UserWorldId = worldId,
                OAuth2Code = oAuth2.OAuth2Code ?? string.Empty,
                ThisUserName = thisUserName,
                ThisUserWorldId = thisUserWorldId,
            };
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.tokenSource.Cancel();
                    this.tokenSource.Dispose();
                    this.chatStream.RequestStream.CompleteAsync().Wait();
                    this.chatStream.Dispose();
                    this.channel.ShutdownAsync().Wait();
                    this.channel.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
