﻿using Discord;
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
    public class MogmogConnectionService
    {
        private const string hostname = "http://localhost:5001";

        public readonly DiscordSocketClient _client;

        private readonly SocketGuildChannel _relayChannel;

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> _chatStream;
        private readonly ChatServiceClient _chatClient;
        private readonly GrpcChannel _channel;

        private readonly Task _runningTask;

        public MogmogConnectionService(DiscordSocketClient client)
        {
            _client = client;

            _relayChannel = _client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("MOGMOG_RELAY_CHANNEL"))) as SocketGuildChannel;

            _channel = GrpcChannel.ForAddress(hostname);
            _chatClient = new ChatServiceClient(_channel);
            _chatStream = _chatClient.Chat();

            _runningTask = ChatLoop();
        }

        public async Task DiscordMessageReceivedAsync(SocketMessage rawMessage)
        {
            if (rawMessage.Channel.Id != _relayChannel.Id) return;
            if (rawMessage.Author.Id == _client.CurrentUser.Id) return;

            Log.Information("({Author}) {Message}", rawMessage.Author.ToString(), rawMessage.Content);

            var chatMessage = new ChatMessage
            {
                Id = rawMessage.Id,
                Content = rawMessage.Content,
                Author = rawMessage.Author.ToString(),
                AuthorId = rawMessage.Author.Id,
                Avatar = rawMessage.Author.GetAvatarUrl(),
                World = null,
                WorldId = (int)PseudoWorld.Discord
            };

            await _chatStream.RequestStream.WriteAsync(chatMessage);
        }

        public async Task GrpcMessageReceivedAsync(ChatMessage chatMessage)
        {
            string rawMessage = $"[{chatMessage.Author}] {chatMessage.Content}";
            await (_relayChannel as ITextChannel).SendMessageAsync(rawMessage);
        }

        private async Task ChatLoop()
        {
            while (true)
            {
                if (await _chatStream.ResponseStream.MoveNext())
                {
                    await GrpcMessageReceivedAsync(_chatStream.ResponseStream.Current);
                }
            }
        }
    }
}
