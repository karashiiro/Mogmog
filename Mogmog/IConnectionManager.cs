using Mogmog.Events;
using Mogmog.Protos;
using System;

namespace Mogmog
{
    public interface IConnectionManager : IDisposable
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        void MessageSend(ChatMessage message, int channelId);

        void AddHost(string hostname, bool saveAccessCode);

        void RemoveHost(string hostname);

        void ReloadHost(string hostname);

        void BanUser(string name, int worldId, string senderName, int senderWorldId, int channelId);

        void UnbanUser(string name, int worldId, string senderName, int senderWorldId, int channelId);

        void TempbanUser(string name, int worldId, DateTime end, string senderName, int senderWorldId, int channelId);

        void KickUser(string name, int worldId, string senderName, int senderWorldId, int channelId);

        void MuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId);

        void UnmuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId);
    }
}
