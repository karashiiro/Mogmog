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

        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public event LogEventHandler LogEvent;

        private readonly AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private readonly ChatServiceClient client;
        private readonly GrpcChannel channel;

        private int channelId;

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
            _ = ChatLoop(this.tokenSource.Token);
        }

        public void Stop()
        {
            this.tokenSource.Cancel();
        }

        private async Task ChatLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await this.chatStream.ResponseStream.MoveNext(cancellationToken))
                    continue;
                MessageReceivedEvent(this, new MessageReceivedEventArgs {
                    Message = chatStream.ResponseStream.Current,
                    ChannelId = this.channelId + 1
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
                    try
                    {
                        Stop();
                    }
                    catch (AggregateException ae)
                    {
                        if (!(ae.InnerException is RpcException))
                            throw;
                        if ((ae.InnerException as RpcException).Status.Detail != "Error starting gRPC call: No connection could be made because the target machine actively refused it.")
                            throw; // This exception is fine to ignore, we don't want to crash everything if the user enters an invalid hostname.
                    }
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
