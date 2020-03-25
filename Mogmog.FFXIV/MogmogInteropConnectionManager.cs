using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IDisposable
    {
        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        private readonly MogmogConfiguration config;
        private readonly Process upgradeLayer;
        private readonly Task runningTask;

        public MogmogInteropConnectionManager(MogmogConfiguration config)
        {
            this.config = config;
            var serializedConfig = JsonConvert.SerializeObject(this.config);
            this.upgradeLayer = Process.Start(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe"), serializedConfig);

            this.runningTask = MessageReceiveLoop();
        }

        private Task MessageReceiveLoop()
        {
            while (true)
            {
                // Scan for something
                ChatMessage message = new ChatMessage();
                int channelId = 0;

                if (true)
                    MessageReceivedDelegate(message, channelId);
            }
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            // Interface with the upgrade layer
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.runningTask.Dispose();
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
}
