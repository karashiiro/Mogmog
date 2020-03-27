using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using System;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnection : IDisposable
    {
        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private readonly ChatServiceClient client;
        private readonly GrpcChannel channel;

        private int channelId;

        private Task runningTask;
        private bool taskActive;

        public MogmogConnection(string hostname, int channelId)
        {
            this.channelId = channelId;

            this.channel = GrpcChannel.ForAddress(hostname);
            this.client = new ChatServiceClient(channel);
            this.chatStream = client.Chat();
        }

        public void SendMessage(ChatMessage message)
        {
            chatStream.RequestStream.WriteAsync(message);
        }

        public void SetChannelId(int newId)
        {
            this.channelId = newId;
        }

        public void Start()
        {
            this.taskActive = true;
            this.runningTask = ChatLoop();
        }

        public void Stop()
        {
            this.taskActive = false;
        }

        private async Task ChatLoop()
        {
            while (this.taskActive)
            {
                Console.WriteLine("boop1");
                if (!await this.chatStream.ResponseStream.MoveNext())
                    continue;
                Console.WriteLine("boop2");
                MessageReceivedDelegate(chatStream.ResponseStream.Current, this.channelId);
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
                    this.taskActive = false;
                    this.runningTask.Dispose();
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
        }
        #endregion
    }
}
