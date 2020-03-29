using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IDisposable
    {
        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

        public delegate void ErrorReceivedCallback(string error);
        public ErrorReceivedCallback ErrorReceivedDelegate;

        private readonly MogmogConfiguration config;
        private readonly Process upgradeLayer;

        public MogmogInteropConnectionManager(MogmogConfiguration config)
        {
            this.config = config;
            var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe");
            var serializedConfig = JsonConvert.SerializeObject(this.config).Replace("\"", "\\\"");
            var startInfo = new ProcessStartInfo(filePath, serializedConfig)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            this.upgradeLayer = Process.Start(startInfo);
            this.upgradeLayer.ErrorDataReceived += UpgradeLayerErrorReceived;
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
            var pack = new GenericInterop
            {
                Command = "AddHost",
                Arg = hostname,
            };
            this.upgradeLayer.StandardInput.WriteLine(JsonConvert.SerializeObject(pack));
        }

        public void RemoveHost(string hostname)
        {
            var pack = new GenericInterop
            {
                Command = "RemoveHost",
                Arg = hostname,
            };
            this.upgradeLayer.StandardInput.WriteLine(JsonConvert.SerializeObject(pack));
        }

        private void UpgradeLayerErrorReceived(object sender, DataReceivedEventArgs e)
        {
            ErrorReceivedDelegate(e.Data);
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
                    this.upgradeLayer.Kill();
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

    struct GenericInterop
    {
        public string Command;
        public string Arg;
    }
}
