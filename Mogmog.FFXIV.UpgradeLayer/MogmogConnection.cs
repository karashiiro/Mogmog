using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Events;
using Mogmog.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnection : IDisposable
    {
        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private readonly CancellationTokenSource tokenSource;
        private readonly GrpcChannel channel;

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public int ChannelId { get; set; }

        public MogmogConnection(string hostname, int channelId)
        {
            this.ChannelId = channelId;
            this.tokenSource = new CancellationTokenSource();

            this.channel = GrpcChannel.ForAddress(hostname);
            var client = new ChatServiceClient(channel);
            var flags = (ServerFlags)client.GetChatServerFlags(new ReqChatServerFlags()).Flags;
            if (flags.HasFlag(ServerFlags.RequiresDiscordOAuth2))
            {
                var oAuth2 = new DiscordOAuth2();
                oAuth2.Authenticate();
                var oAuth2Code = oAuth2.OAuth2Code;
                client.SendOAuth2Code(new ReqOAuth2Code { OAuth2Code = oAuth2Code });
            }
            this.chatStream = client.Chat(new CallOptions()
                .WithCancellationToken(this.tokenSource.Token)
                .WithDeadline(DateTime.UtcNow.AddMinutes(1))
                .WithWaitForReady());

            _ = ChatLoop(this.tokenSource.Token);
        }
        
        public void SendMessage(ChatMessage message)
        {
            chatStream.RequestStream.WriteAsync(message);
        }

        private async Task ChatLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await this.chatStream.ResponseStream.MoveNext(cancellationToken))
                    continue;
                MessageReceivedEvent(this, new MessageReceivedEventArgs {
                    Message = chatStream.ResponseStream.Current,
                    ChannelId = this.ChannelId + 1
                });
            }
        }

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
