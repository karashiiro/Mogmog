using Mogmog.Protos;
using System;
using System.Linq;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }
    }

    public class MogmogConnectionManager : IDisposable
    {
        private readonly DisposableStrongIndexedList<MogmogConnection> connections;

        private readonly MogmogConfiguration config;

        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event MessageReceivedEventHandler MessageReceivedEvent;

        public MogmogConnectionManager(MogmogConfiguration config)
        {
            this.config = config;
            this.connections = new DisposableStrongIndexedList<MogmogConnection>();

            foreach (string hostname in config.Hostnames)
            {
                if (string.IsNullOrEmpty(hostname))
                {
                    this.connections.Append(null);
                }
                else
                {
                    var connection = new MogmogConnection(hostname, this.connections.Count)
                    {
                        MessageReceivedDelegate = MessageReceived
                    };
                    this.connections.Append(connection);
                    connection.Start();
                }
            }
        }

        public void AddHost(string hostname)
        {
            if (this.config.Hostnames.Contains(hostname))
                return; // Should send back an error message or something eventually.
            this.config.Hostnames.Add(hostname);
            var connection = new MogmogConnection(hostname, this.config.Hostnames.IndexOf(hostname))
            {
                MessageReceivedDelegate = MessageReceived
            };
            connection.Start();
            this.connections.Add(connection);
        }

        public void RemoveHost(string hostname)
        {
            int i = this.config.Hostnames.IndexOf(hostname);
            if (i == -1)
                return;
            this.config.Hostnames.RemoveAt(i);
            this.connections.RemoveAt(i);
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            if (this.connections.Count <= channelId)
                return;
            if (channelId < 0)
                return;
            if (this.connections[channelId] == null) // Shouldn't happen but might, should return an error message
                return;
            this.connections[channelId].SendMessage(message);
        }

        private void MessageReceived(ChatMessage message, int channelId)
        {
            MessageReceivedEvent(this, new MessageReceivedEventArgs { Message = message, ChannelId = channelId });
        }

        public void Dispose()
        {
            this.connections.Dispose();
        }
    }
}
