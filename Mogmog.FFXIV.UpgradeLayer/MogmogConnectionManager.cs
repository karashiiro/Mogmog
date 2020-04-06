using Mogmog.Protos;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnectionManager : IDisposable
    {
        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event MessageReceivedEventHandler MessageReceivedEvent;

        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public event LogEventHandler LogEvent;

        private readonly DisposableStrongIndexedList<MogmogConnection> connections;

        private readonly MogmogConfiguration config;

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reference maintained in List.")]
        public MogmogConnectionManager(MogmogConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.connections = new DisposableStrongIndexedList<MogmogConnection>();

            foreach (string hostname in config.Hostnames)
            {
                if (string.IsNullOrEmpty(hostname))
                {
                    this.connections.Append(null);
                }
                else
                {
                    var connection = new MogmogConnection(hostname, this.connections.Count);
                    connection.MessageReceivedEvent += MessageReceived;
                    this.connections.Append(connection);
                }
            }
        }

        public void AddHost(string hostname)
        {
            if (this.config.Hostnames.Contains(hostname))
                return; // Should send back an error message or something eventually.
            this.config.Hostnames.Add(hostname);
            var connection = new MogmogConnection(hostname, this.config.Hostnames.IndexOf(hostname));
            connection.MessageReceivedEvent += MessageReceived;
            connection.LogEvent += Log;
            this.connections.Add(connection);
        }

        public void RemoveHost(string hostname)
        {
            int i = this.config.Hostnames.IndexOf(hostname);
            if (i == -1)
                return;
            this.config.Hostnames.RemoveAt(i);
            this.connections[i].MessageReceivedEvent -= MessageReceived;
            this.connections[i].LogEvent -= Log;
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

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            MessageReceivedEvent(sender, e);
        }

        private void Log(object sender, LogEventArgs e)
        {
            LogEvent(sender, e);
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.connections.Dispose();
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
