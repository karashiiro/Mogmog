using Mogmog.Protos;
using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnectionManager : IDisposable
    {
        private readonly IList<MogmogConnection> connections;

        private readonly MogmogConfiguration config;

        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        public MogmogConnectionManager(MogmogConfiguration config)
        {
            this.connections = new List<MogmogConnection>();

            this.config = config;

            foreach (string hostname in config.Hostnames)
            {
                if (string.IsNullOrEmpty(hostname))
                {
                    this.connections.Add(null);
                }
                else
                {
                    var connection = new MogmogConnection(hostname, this.connections.Count)
                    {
                        MessageReceivedDelegate = MessageReceived
                    };
                    this.connections.Add(connection);
                }
            }
        }

        public void AddHost(string hostname)
        {
            int ni = this.config.Hostnames.IndexOf(null);
            if (ni != -1)
            {
                this.config.Hostnames.Remove(null);
                this.config.Hostnames.Insert(ni, hostname);
                this.connections.Remove(null);

                var connection = new MogmogConnection(hostname, ni)
                {
                    MessageReceivedDelegate = MessageReceived
                };
                this.connections.Insert(ni, connection);
            }
            else
            {
                this.config.Hostnames.Add(hostname);
                var connection = new MogmogConnection(hostname, this.connections.Count)
                {
                    MessageReceivedDelegate = MessageReceived
                };
                this.connections.Add(connection);
            }
        }

        public void RemoveHost(string hostname)
        {
            int i = this.config.Hostnames.IndexOf(hostname);
            if (i == -1)
                return;
            this.config.Hostnames.RemoveAt(i);
            this.config.Hostnames.Insert(i, null);
            this.connections.RemoveAt(i);
            this.connections.Insert(i, null);
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            if (this.connections[channelId] == null) // Shouldn't happen but might, should return an error message
                return;
            this.connections[channelId].SendMessage(message);
        }

        public void MessageReceived(ChatMessage message, int channelId)
        {
            MessageReceivedDelegate(message, channelId);
        }

        public void Dispose()
        {
            foreach (var connection in this.connections)
            {
                connection.Dispose();
            }
        }
    }
}
