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
    public class MogmogConnectionService : IAsyncDisposable
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
            => await _chatClient.BotOpUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

        public async Task BanUserAsync(IGuildUser user)
            => await _chatClient.BotBanUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

        public async Task UnbanUserAsync(IGuildUser user)
            => await _chatClient.BotUnbanUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

        public async Task TempbanUserAsync(IGuildUser user, DateTime end)
            => await _chatClient.BotTempbanUserAsync(new TempbanUserBotRequest { Id = user.Id, UnbanTimestamp = end.ToBinary(), StateKey = _stateKey });

        public async Task KickUserAsync(IGuildUser user)
            => await _chatClient.BotKickUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

        public async Task MuteUserAsync(IGuildUser user)
            => await _chatClient.BotMuteUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

        public async Task UnmuteUserAsync(IGuildUser user)
            => await _chatClient.BotUnmuteUserAsync(new UserActionBotRequest { Id = user.Id, StateKey = _stateKey });

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

        private async Task GrpcMessageReceivedAsync(ChatMessage chatMessage)
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
        private bool disposedValue; // To detect redundant calls

        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                    await _chatStream.RequestStream.CompleteAsync();
                    await _channel.ShutdownAsync();
                }

                disposedValue = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
