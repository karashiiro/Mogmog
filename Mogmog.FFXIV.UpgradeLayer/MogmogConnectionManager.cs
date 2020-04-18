using Grpc.Core;
using Mogmog.Events;
using Mogmog.Exceptions;
using Mogmog.Logging;
using Mogmog.Protos;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class MogmogConnectionManager : IConnectionManager, IDisposable
    {
        private readonly DisposableStrongIndexedList<MogmogConnection> connections;

        private readonly MogmogConfiguration config;

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reference maintained in List.")]
        public MogmogConnectionManager(MogmogConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.connections = new DisposableStrongIndexedList<MogmogConnection>();

            foreach (var host in config.Hosts)
            {
                if (string.IsNullOrEmpty(host.Hostname))
                {
                    this.connections.Append(null);
                }
                else
                {
                    var connection = new MogmogConnection(host.Hostname, this.connections.Count, host.SaveAccessCode);
                    connection.MessageReceivedEvent += MessageReceived;
                    this.connections.Append(connection);
                }
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposal responsibility belongs to the connection manager.")]
        public void AddHost(string hostname, bool saveAccessCode)
        {
            if (this.config.Hosts.FirstOrDefault(h => h.Hostname == hostname) == null)
            {
                Mogger.LogError(LogMessages.HostExists);
                return;
            }
            var host = new Host { Hostname = hostname, SaveAccessCode = saveAccessCode };
            this.config.Hosts.Add(host);
            MogmogConnection connection;
            try
            {
                connection = new MogmogConnection(hostname, this.config.Hosts.IndexOf(host), saveAccessCode);
            }
            catch (RpcException e)
            {
                this.config.Hosts.Remove(host);
                if (e.Status.Detail == "Error starting gRPC call: No connection could be made because the target machine actively refused it.")
                    Mogger.LogError(LogMessages.HostOffline); // A more user-friendly error
                else
                    Mogger.LogError(e.Message);
                return;
            }
            connection.MessageReceivedEvent += MessageReceived;
            this.connections.Add(connection);
        }

        public void RemoveHost(string hostname)
        {
            var host = this.config.Hosts.FirstOrDefault(h => h.Hostname == hostname);
            var i = this.config.Hosts.IndexOf(host);
            if (host == null || i == -1)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }

            this.config.Hosts.RemoveAt(i);
            this.connections[i].MessageReceivedEvent -= MessageReceived;
            this.connections.RemoveAt(i);
        }

        public void ReloadHost(string hostname)
        {
            var host = this.config.Hosts.FirstOrDefault(h => h.Hostname == hostname);
            var i = this.config.Hosts.IndexOf(host);
            if (host == null || i == -1)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }

            var saveAccessCode = this.connections[i].SaveAccessCode;
            var channelId = this.connections[i].ChannelId;
            this.connections[i].Dispose();
            this.connections[i] = new MogmogConnection(hostname, channelId, saveAccessCode);
        }

        #region Moderation Commands
        public void BanUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].BanUser(name, worldId, senderName, senderWorldId);
        }

        public void UnbanUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].UnbanUser(name, worldId, senderName, senderWorldId);
        }

        public void TempbanUser(string name, int worldId, DateTime end, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].TempbanUser(name, worldId, end, senderName, senderWorldId);
        }

        public void KickUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].KickUser(name, worldId, senderName, senderWorldId);
        }

        public void MuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].MuteUser(name, worldId, senderName, senderWorldId);
        }

        public void UnmuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
        {
            if (this.connections[channelId] == null)
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            _ = this.connections[channelId].UnmuteUser(name, worldId, senderName, senderWorldId);
        }
        #endregion

        public void MessageSend(ChatMessage message, int channelId)
        {
            if (this.connections.Count <= channelId || channelId < 0)
            {
                Mogger.LogError(LogMessages.HostNotFound + $" {this.connections.Count} connections found.");
                return;
            }
            if (this.connections[channelId] == null) // Shouldn't happen but might
            {
                Mogger.LogError(LogMessages.HostNotFound);
                return;
            }
            this.connections[channelId].SendMessage(message);
        }

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var handler = MessageReceivedEvent;
            handler?.Invoke(sender, e);
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
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
