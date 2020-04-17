using Discord;
using Discord.WebSocket;
using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Grpc.Core.Metadata;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Discord.Services
{
    public class MogmogConnectionService : IDisposable
    {
        private const string hostname = "https://localhost:5001";

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> _chatStream;
        private readonly CancellationTokenSource _tokenSource;
        private readonly ChatServiceClient _chatClient;
        private readonly DiscordSocketClient _client;
        private readonly GrpcChannel _channel;
        private readonly string _stateKey;

        public ServerFlags Flags { get; set; }
        private SocketGuildChannel RelayChannel { get => _client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("MOGMOG_RELAY_CHANNEL"))) as SocketGuildChannel; }

        public MogmogConnectionService(DiscordSocketClient client)
        {
            _client = client;
            _channel = GrpcChannel.ForAddress(hostname);
            _chatClient = new ChatServiceClient(_channel);
            var callOptions = new CallOptions()
                .WithDeadline(DateTime.UtcNow.AddMinutes(1))
                .WithWaitForReady();
            Flags = (ServerFlags)_chatClient.GetChatServerInfo(new ReqChatServerInfo()).Flags;
            if (Flags.HasFlag(ServerFlags.RequiresDiscordOAuth2))
            {
                _stateKey = OAuth2Utils.GenerateStateString(100);
                File.WriteAllText("identifier", _stateKey);
                var headers = new Metadata
                {
                    new Entry("code", _stateKey),
                    new Entry("name", _client.CurrentUser.ToString()),
                    new Entry("worldId", ((int)PseudoWorld.Discord).ToString()),
                };
                callOptions = callOptions.WithHeaders(headers);
            }
            _chatStream = _chatClient.Chat(callOptions);
            _tokenSource = new CancellationTokenSource();
            _ = ChatLoop(_tokenSource.Token);
        }

        public async Task OpUserAsync(IGuildUser user)
            => await _chatClient.OpDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task BanUserAsync(IGuildUser user)
            => await _chatClient.BanDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task UnbanUserAsync(IGuildUser user)
            => await _chatClient.UnbanDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task TempbanUserAsync(IGuildUser user, DateTime end)
            => await _chatClient.TempbanDiscordUserAsync(new ReqTempbanDiscordUser { Id = user.Id, UnbanTimestamp = end.ToBinary(), StateKey = _stateKey });

        public async Task KickUserAsync(IGuildUser user)
            => await _chatClient.KickDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task MuteUserAsync(IGuildUser user)
            => await _chatClient.MuteDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task UnmuteUserAsync(IGuildUser user)
            => await _chatClient.UnmuteDiscordUserAsync(new UserDiscordActionRequest { Id = user.Id, StateKey = _stateKey });

        public async Task DiscordMessageReceivedAsync(SocketMessage rawMessage)
        {
            if (!(rawMessage.Channel is SocketGuildChannel)) return;
            var guildChannel = rawMessage.Channel as SocketGuildChannel;

            if (guildChannel.Id != RelayChannel.Id) return;
            if (rawMessage.Author.Id == _client.CurrentUser.Id) return;

            var memberName = guildChannel.Guild.GetUser(rawMessage.Author.Id).Nickname ?? rawMessage.Author.ToString();

            Log.Information("({Author}) {Message}", memberName, rawMessage.Content);

            var chatMessage = new ChatMessage
            {
                Id = rawMessage.Id,
                Content = rawMessage.Content,
                Author = memberName,
                AuthorId = rawMessage.Author.Id,
                AuthorId2 = _client.CurrentUser.Id,
                Avatar = rawMessage.Author.GetAvatarUrl(),
                World = string.Empty,
                WorldId = (int)PseudoWorld.Discord,
            };

            await _chatStream.RequestStream.WriteAsync(chatMessage);
        }

        public async Task GrpcMessageReceivedAsync(ChatMessage chatMessage)
        {
            if (chatMessage.AuthorId2 == _client.CurrentUser.Id)
                return;

            var messageAuthor = new EmbedAuthorBuilder()
                .WithName($"({chatMessage.World}) {chatMessage.Author}")
                .WithIconUrl(chatMessage.Avatar);
            var messageEmbed = new EmbedBuilder()
                .WithAuthor(messageAuthor)
                .WithDescription(chatMessage.Content)
                .WithColor(Color.Gold)
                .Build();
            await (RelayChannel as ITextChannel).SendMessageAsync(embed: messageEmbed);
        }

        private async Task ChatLoop(CancellationToken token)
        {
            while (await _chatStream.ResponseStream.MoveNext(token))
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                await GrpcMessageReceivedAsync(_chatStream.ResponseStream.Current);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                    _chatStream.RequestStream.CompleteAsync().Wait();
                    _channel.ShutdownAsync().Wait();
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
