using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV
{
    public class MogmogConnectionManager : IDisposable
    {
        private readonly IList<AsyncDuplexStreamingCall<ChatMessage, ChatMessage>> chatStreams;
        private readonly IList<ChatServiceClient> clients;
        private readonly IList<GrpcChannel> channels;

        private readonly MogmogConfiguration config;

        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        private readonly Task runningTask;

        public MogmogConnectionManager(MogmogConfiguration config)
        {
            this.chatStreams = new List<AsyncDuplexStreamingCall<ChatMessage, ChatMessage>>();
            this.clients = new List<ChatServiceClient>();
            this.channels = new List<GrpcChannel>();

            this.config = config;

            foreach (string hostname in config.Hostnames)
            {
                if (string.IsNullOrEmpty(hostname))
                {
                    this.channels.Add(null);
                    this.clients.Add(null);
                    this.chatStreams.Add(null);
                }
                else
                {
                    var channel = GrpcChannel.ForAddress(hostname);
                    var client = new ChatServiceClient(channel);
                    var chatStream = client.Chat();

                    this.channels.Add(channel);
                    this.clients.Add(client);
                    this.chatStreams.Add(chatStream);
                }
            }

            this.runningTask = ChatLoop();
        }

        public void AddHost(string hostname)
        {
            this.config.Hostnames.Add(hostname);

            var channel = GrpcChannel.ForAddress(hostname);
            var client = new ChatServiceClient(channel);
            var chatStream = client.Chat();

            this.channels.Add(channel);
            this.clients.Add(client);
            this.chatStreams.Add(chatStream);
        }

        public void RemoveHost(string hostname)
        {
            int i = this.config.Hostnames.IndexOf(hostname);
            this.config.Hostnames[i] = string.Empty;

            this.chatStreams[i].Dispose();
            this.chatStreams[i] = null;

            this.channels[i].Dispose();
            this.channels[i] = null;
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            if (this.chatStreams[channelId] == null)
                return;
            this.chatStreams[channelId].RequestStream.WriteAsync(message);
        }

        private async Task ChatLoop()
        {
            while (true)
            {
                for (int i = 0; i < this.chatStreams.Count; i++)
                {
                    if (this.chatStreams[i] == null)
                        continue;
                    if (await this.chatStreams[i].ResponseStream.MoveNext())
                    {
                        MessageReceivedDelegate(this.chatStreams[i].ResponseStream.Current, i);
                    }
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < this.chatStreams.Count; i++)
            {
                this.chatStreams[i].Dispose();
                this.channels[i].Dispose();
            }

            this.runningTask.Dispose();
        }
    }
}
