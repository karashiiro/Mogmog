﻿using Mogmog.Events;
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
        public event EventHandler<LogEventArgs> LogEvent;

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

        public void AddHost(string hostname, string oAuth2Code = null)
        {
            if (this.config.Hostnames.Contains(hostname))
            {
                LogError("The provided host already exists.");
                return;
            }
            this.config.Hostnames.Add(hostname);
            var connection = new MogmogConnection(hostname, this.config.Hostnames.IndexOf(hostname), oAuth2Code);
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

        public void ReloadHost(string hostname)
        {
            int i = this.config.Hostnames.IndexOf(hostname);
            if (i == -1)
                return;
            var channelId = this.connections[i].ChannelId;
            this.connections[i].Dispose();
            this.connections[i] = new MogmogConnection(hostname, channelId);
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            if (this.connections.Count <= channelId || channelId < 0)
                return;
            if (this.connections[channelId] == null) // Shouldn't happen but might
            {
                LogError("The provided host was not found! This should not happen; please report this issue.");
                return;
            }
            this.connections[channelId].SendMessage(message);
        }

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var handler = MessageReceivedEvent;
            handler?.Invoke(sender, e);
        }

        private void Log(object sender, LogEventArgs e)
        {
            var handler = LogEvent;
            handler?.Invoke(sender, e);
        }

        private void Log(string message, bool isError = false)
            => Log(this, new LogEventArgs { LogMessage = message, IsError = isError });

        private void LogError(string message)
            => Log(message, true);

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
