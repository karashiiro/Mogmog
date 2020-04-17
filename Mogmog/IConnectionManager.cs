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

        void BanUser(string name, int worldId, int channelId);

        void UnbanUser(string name, int worldId, int channelId);

        void TempbanUser(string name, int worldId, int channelId, DateTime end);

        void KickUser(string name, int worldId, int channelId);

        void MuteUser(string name, int worldId, int channelId);

        void UnmuteUser(string name, int worldId, int channelId);
    }
}
