using Mogmog.Events;
using Mogmog.Protos;
using System;

namespace Mogmog
{
    public interface IConnectionManager : IDisposable
    {
        event EventHandler<LogEventArgs> LogEvent;

        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        void MessageSend(ChatMessage message, int channelId);

        void AddHost(string hostname, string oAuth2Code);

        void RemoveHost(string hostname);

        void ReloadHost(string hostname);
    }
}
