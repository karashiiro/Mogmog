using Mogmog.Protos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mogmog.Server.Services
{
    public class MogmogTransmissionService : IDisposable
    {
        public delegate void MessageEventHandler(object sender, MessageEventArgs e);
        public event MessageEventHandler MessageSent;

        private readonly Queue<ChatMessage> _messageQueue;

#pragma warning disable IDE0052 // Remove unread private members
        private readonly Task _runningTask;
#pragma warning restore IDE0052 // Remove unread private members
        private bool _taskActive;

        public MogmogTransmissionService()
        {
            _messageQueue = new Queue<ChatMessage>();

            _taskActive = true;
            _runningTask = EventLoop();
        }

        public void Send(ChatMessage message)
            => _messageQueue.Enqueue(message);

        private async Task EventLoop()
        {
            while (_taskActive)
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
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _taskActive = false;
                }

                disposedValue = true;
            }
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            Dispose(true);
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
