using Mogmog.Protos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Server.Services
{
    public class MogmogTransmissionService : IDisposable
    {
        private readonly Queue<ChatMessage> _messageQueue;

        private Task _runningTask;
        private CancellationTokenSource _tokenSource;

        public event EventHandler<MessageEventArgs> MessageSent;

        public MogmogTransmissionService()
        {
            _messageQueue = new Queue<ChatMessage>();
            Start();
        }

        public void Send(ChatMessage message)
            => _messageQueue.Enqueue(message);

        public Task Start()
        {
            _tokenSource = new CancellationTokenSource();
            _runningTask = Task.WhenAny(EventLoop(), Task.Run(() =>
            {
                while (true)
                {
                    _tokenSource.Token.ThrowIfCancellationRequested();
                }
            }));
            
            return Task.CompletedTask;
        }

        private async Task Stop()
        {
            _tokenSource.Cancel();
            await _runningTask;
        }

        private async Task EventLoop()
        {
            while (true)
            {
                if (_messageQueue.Count == 0)
                {
                    await Task.Delay(50);
                    continue;
                }
                MessageSent(this, new MessageEventArgs(_messageQueue.Dequeue()));
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
                    Stop().Wait();
                    _runningTask.Dispose();
                    _tokenSource.Dispose();
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

    public class MessageEventArgs : EventArgs
    {
        public ChatMessage Message { get; private set; }

        public MessageEventArgs(ChatMessage message)
        {
            Message = message;
        }
    }
}
