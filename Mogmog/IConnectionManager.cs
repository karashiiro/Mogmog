using Mogmog.Events;
using Mogmog.Protos;
using System;

namespace Mogmog
{
    public interface IConnectionManager : IDisposable
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        void MessageSend(ChatMessage message, int channelId);

        void AddHost(string hostname);

        void RemoveHost(string hostname);

        void ReloadHost(string hostname);
    }
}
