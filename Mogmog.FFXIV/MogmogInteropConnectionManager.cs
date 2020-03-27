using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IDisposable
    {
        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        private readonly MogmogConfiguration config;
        private readonly Process upgradeLayer;

        public MogmogInteropConnectionManager(MogmogConfiguration config)
        {
            this.config = config;
            var serializedConfig = JsonConvert.SerializeObject(this.config);
            this.upgradeLayer = Process.Start(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe"), serializedConfig);
            this.upgradeLayer.OutputDataReceived += UpgradeLayerMessageReceived;
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            var pack = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            this.upgradeLayer.StandardInput.WriteLine(JsonConvert.SerializeObject(pack));
        }

        public void AddHost(string hostname)
        {
            var pack = new AddHostInterop
            {
                HostAdd = hostname,
            };
            this.upgradeLayer.StandardInput.WriteLine(JsonConvert.SerializeObject(pack));
        }

        public void RemoveHost(string hostname)
        {
            var pack = new RemoveHostInterop
            {
                HostRemove = hostname,
            };
            this.upgradeLayer.StandardInput.WriteLine(JsonConvert.SerializeObject(pack));
        }

        private void UpgradeLayerMessageReceived(object sender, DataReceivedEventArgs e)
        {
            var messageInterop = JsonConvert.DeserializeObject<ChatMessageInterop>(e.Data);
            var message = messageInterop.Message;
            var channelId = messageInterop.ChannelId;
            MessageReceivedDelegate(message, channelId);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.upgradeLayer.Dispose();
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

    struct ChatMessageInterop
    {
        public ChatMessage Message;
        public int ChannelId;
    }

    struct AddHostInterop
    {
        public string HostAdd;
    }

    struct RemoveHostInterop
    {
        public string HostRemove;
    }
}
