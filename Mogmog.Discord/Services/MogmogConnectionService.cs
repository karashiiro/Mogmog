using Discord;
using Discord.WebSocket;
using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using Serilog;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Grpc.Core.Metadata;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Discord.Services
{
    public class MogmogConnectionService : IDisposable
    {
        private const string hostname = "https://localhost:5001";

        private readonly DiscordSocketClient _client;

        private SocketGuildChannel RelayChannel { get => _client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("MOGMOG_RELAY_CHANNEL"))) as SocketGuildChannel; }

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> _chatStream;
        private readonly GrpcChannel _channel;

        private readonly Task _runningTask;

        public MogmogConnectionService(DiscordSocketClient client)
        {
            _client = client;
            
            _channel = GrpcChannel.ForAddress(hostname);
            var chatClient = new ChatServiceClient(_channel);
            var callOptions = new CallOptions()
                .WithDeadline(DateTime.UtcNow.AddMinutes(1))
                .WithWaitForReady();
            var flags = (ServerFlags)chatClient.GetChatServerInfo(new ReqChatServerInfo()).Flags;
            if (flags.HasFlag(ServerFlags.RequiresDiscordOAuth2))
            {
                var stateString = OAuth2Utils.GenerateStateString(100);
                File.WriteAllText("identifier", stateString);
                var headers = new Metadata
                {
                    new Entry("code", stateString),
                };
                callOptions = callOptions.WithHeaders(headers);
            }
            _chatStream = chatClient.Chat(callOptions);

            _runningTask = ChatLoop();
        }

        public void Dispose()
        {
            _chatStream.RequestStream.CompleteAsync().Wait();
            _channel.ShutdownAsync().Wait();
        }

        public bool IsActive() => _runningTask.Status == TaskStatus.Running;

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
                WorldId = (int)PseudoWorld.Discord
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

        private async Task ChatLoop()
        {
            while (await _chatStream.ResponseStream.MoveNext())
            {
                await GrpcMessageReceivedAsync(_chatStream.ResponseStream.Current);
            }
        }
    }
}
