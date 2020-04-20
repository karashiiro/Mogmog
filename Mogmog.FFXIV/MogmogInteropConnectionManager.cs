using Mogmog.Events;
using Mogmog.Logging;
using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using SimpleIPCHttp;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IConnectionManager, IDisposable
    {
        private readonly IpcInterface ipc;
        private readonly HttpClient client;
        private readonly Process upgradeLayer;

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public MogmogInteropConnectionManager(MogmogConfiguration config, HttpClient client)
        {
            this.client = client;
            this.ipc = new IpcInterface(client);

            this.ipc.On<ChatMessageInterop>(ChatMessageReceived);
            this.ipc.On<GenericInterop>(LogMessageReceived);

            var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe");
            var serializedConfig = JsonConvert.SerializeObject(config).Replace("\"", "\\\"");
            var args = $"{serializedConfig} {this.ipc.PartnerPort} {this.ipc.Port}";
            var startInfo = new ProcessStartInfo(filePath, string.Join(" ", args))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            this.upgradeLayer = Process.Start(startInfo);
        }

        public void MessageSend(ChatMessage message, int channelId)
            => _ = SendToUpgradeLayer(message, channelId);

        public void AddHost(string hostname, bool saveAccessCode)
            => _ = SendToUpgradeLayer(ClientOpcode.AddHost, $"{hostname} {saveAccessCode}");

        public void RemoveHost(string hostname)
            => _ = SendToUpgradeLayer(ClientOpcode.RemoveHost, hostname);

        public void ReloadHost(string hostname)
            => _ = SendToUpgradeLayer(ClientOpcode.ReloadHost, hostname);

        public void BanUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.BanUser, $"{name} {worldId} {senderName} {senderWorldId} {channelId}");

        public void UnbanUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.UnbanUser, $"{name} {worldId} {senderName} {senderWorldId} {channelId}");

        public void TempbanUser(string name, int worldId, DateTime end, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.TempbanUser, $"{name} {worldId} {end.ToBinary()} {senderName} {senderWorldId} {channelId}");

        public void KickUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.KickUser, $"{name} {worldId} {senderName} {senderWorldId} {channelId}");

        public void MuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.MuteUser, $"{name} {worldId} {senderName} {senderWorldId} {channelId}");

        public void UnmuteUser(string name, int worldId, string senderName, int senderWorldId, int channelId)
            => _ = SendToUpgradeLayer(ClientOpcode.UnmuteUser, $"{name} {worldId} {senderName} {senderWorldId} {channelId}");

        #region Interop Interface Methods
        public void ChatMessageReceived(ChatMessageInterop chatMessage)
        {
            var message = chatMessage.Message;
            var channelId = chatMessage.ChannelId;
            var handler = MessageReceivedEvent;
            handler?.Invoke(this, new MessageReceivedEventArgs { Message = message, ChannelId = channelId });
        }

        public static void LogMessageReceived(GenericInterop logInfo)
        {
            if (bool.Parse(logInfo.Arg))
                Mogger.LogError(logInfo.Command);
            else
                Mogger.Log(logInfo.Command);
        }

        public Task SendToUpgradeLayer(ClientOpcode command, string arg)
        {
            var pack = new GenericInterop
            {
                Command = command.ToString(),
                Arg = arg,
            };
            return this.ipc.SendMessage(pack);
        }

        private Task SendToUpgradeLayer(ChatMessage message, int channelId)
        {
            var pack = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            return this.ipc.SendMessage(pack);
        }

        #if DEBUG
        public IntPtr GetMainWindowHandle()
        {
            return this.upgradeLayer.MainWindowHandle;
        }
        #endif
        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.ipc.Dispose();
                    
                    this.upgradeLayer.WaitForExit();
                    this.upgradeLayer.Dispose();
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
