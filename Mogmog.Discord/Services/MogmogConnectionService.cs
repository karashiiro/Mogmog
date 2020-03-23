using Discord;
using Discord.WebSocket;
using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using Serilog;
using System;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Discord.Services
{
    public class MogmogConnectionService : IDisposable
    {
        private const string hostname = "https://localhost:5001";

        public readonly DiscordSocketClient _client;

        private SocketGuildChannel RelayChannel { get => _client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("MOGMOG_RELAY_CHANNEL"))) as SocketGuildChannel; }

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> _chatStream;
        private readonly ChatServiceClient _chatClient;
        private readonly GrpcChannel _channel;

        private readonly Task _runningTask;

        public MogmogConnectionService(DiscordSocketClient client)
        {
            _client = client;
            
            _channel = GrpcChannel.ForAddress(hostname);
            _chatClient = new ChatServiceClient(_channel);
            _chatStream = _chatClient.Chat();

            _runningTask = ChatLoop();
        }

        public void Dispose()
        {
            _runningTask.Dispose();
            _chatStream.RequestStream.CompleteAsync().Wait();
            _channel.Dispose();
        }

        public bool IsActive() => _runningTask.Status == TaskStatus.Running;

        public async Task DiscordMessageReceivedAsync(SocketMessage rawMessage)
        {
            if (!(rawMessage.Channel is SocketGuildChannel)) return;
            if ((rawMessage.Channel as SocketGuildChannel).Id != RelayChannel.Id) return;
            if (rawMessage.Author.Id == _client.CurrentUser.Id) return;

            Log.Information("({Author}) {Message}", rawMessage.Author.ToString(), rawMessage.Content);

            var chatMessage = new ChatMessage
            {
                Id = rawMessage.Id,
                Content = rawMessage.Content,
                Author = rawMessage.Author.ToString(),
                AuthorId = rawMessage.Author.Id,
                Avatar = rawMessage.Author.GetAvatarUrl(),
                World = string.Empty,
                WorldId = (int)PseudoWorld.Discord
            };

            await _chatStream.RequestStream.WriteAsync(chatMessage);
        }

        public async Task GrpcMessageReceivedAsync(ChatMessage chatMessage)
        {
            /*if (chatMessage.WorldId == (int)PseudoWorld.Discord)
                return;*/
            string rawMessage = $"[{chatMessage.Author}] {chatMessage.Content}";
            await (RelayChannel as ITextChannel).SendMessageAsync(rawMessage);
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
