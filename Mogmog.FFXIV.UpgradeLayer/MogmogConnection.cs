using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }
    }

    public class MogmogConnection : IDisposable
    {
        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event MessageReceivedEventHandler MessageReceivedEvent;

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private readonly ChatServiceClient client;
        private readonly GrpcChannel channel;

        private int channelId;

        private Task runningTask;
        private CancellationTokenSource tokenSource;

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
            this.tokenSource = new CancellationTokenSource();
            this.runningTask = Task.WhenAny(ChatLoop(), Task.Run(() =>
            {
                while (true)
                {
                    this.tokenSource.Token.ThrowIfCancellationRequested();
                }
            }));
        }

        public async Task Stop()
        {
            this.tokenSource.Cancel();
            await this.runningTask;
        }

        private async Task ChatLoop()
        {
            while (true)
            {
                if (!await this.chatStream.ResponseStream.MoveNext())
                    continue;
                MessageReceivedEvent(this, new MessageReceivedEventArgs {
                    Message = chatStream.ResponseStream.Current,
                    ChannelId = this.channelId + 1
                });
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
                    Stop().Wait();
                    this.runningTask.Dispose();
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
        }
        #endregion
    }
}
