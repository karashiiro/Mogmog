using Mogmog.Events;
using Mogmog.Protos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PeanutButter.SimpleHTTPServer;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IDisposable
    {
        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;
        public event EventHandler<LogEventArgs> LogEvent;

        private readonly HttpClient client;
        private readonly HttpServer server;
        private readonly MogmogConfiguration config;
        private readonly Process upgradeLayer;
        private readonly Uri localhost;

        public MogmogInteropConnectionManager(MogmogConfiguration config, HttpClient client)
        {
            this.client = client;
            this.server = new HttpServer();

            this.server.AddJsonDocumentHandler((processor, stream) => UpgradeLayerMessageReceived(stream));

            this.localhost = new Uri($"http://localhost:{this.server.Port + 1}");

            this.config = config;

            var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe");
            var serializedConfig = JsonConvert.SerializeObject(this.config).Replace("\"", "\\\"");
            var args = new string[] { serializedConfig, this.server.Port.ToString(CultureInfo.InvariantCulture) };
            var startInfo = new ProcessStartInfo(filePath, string.Join(" ", args))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            this.upgradeLayer = Process.Start(startInfo);
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            _ = SendToUpgradeLayer(message, channelId);
        }

        public void AddHost(string hostname)
        {
            this.config.Hostnames.Add(hostname);
            _ = SendToUpgradeLayer("AddHost", hostname);
        }

        public void RemoveHost(string hostname)
        {
            this.config.Hostnames.Remove(hostname);
            _ = SendToUpgradeLayer("RemoveHost", hostname);
        }

        public void ReloadHost(string hostname)
        {
            _ = SendToUpgradeLayer("ReloadHost", hostname);
        }

        #region Interop Interface Methods
        public byte[] UpgradeLayerMessageReceived(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var data = Encoding.UTF8.GetString(memoryStream.GetBuffer());

            JToken messageInterop;
            try
            {
                messageInterop = JObject.Parse(data);
            }
            catch (JsonReaderException e)
            {
                return Encoding.UTF8.GetBytes(e.Message);
            }

            if (messageInterop["Message"] != null) // Jank but whatever, ripping all this out once Dalamud on .NET Core is released
            {
                var chatMessage = messageInterop.ToObject<ChatMessageInterop>();
                var message = chatMessage.Message;
                var channelId = chatMessage.ChannelId;
                try
                {
                    MessageReceivedEvent(this, new MessageReceivedEventArgs { Message = message, ChannelId = channelId });
                }
                catch (NullReferenceException)
                {
                    return Array.Empty<byte>(); // No callbacks set.
                }
            }
            else
            {
                var logInfo = messageInterop.ToObject<GenericInterop>();
                try
                {
                    LogEvent(this, new LogEventArgs { LogMessage = logInfo.Command, IsError = bool.Parse(logInfo.Arg) });
                }
                catch (NullReferenceException)
                {
                    return Array.Empty<byte>(); // No callbacks set.
                }
            }

            return Array.Empty<byte>();
        }

        public async Task SendToUpgradeLayer(string command, string arg)
        {
            var pack = new GenericInterop
            {
                Command = command,
                Arg = arg,
            };
            using var messageBytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pack)));
            await this.client.PostAsync(localhost, messageBytes); // Call must be awaited to avoid losing scope of the byte array.
        }

        private async Task SendToUpgradeLayer(ChatMessage message, int channelId)
        {
            var pack = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            using var messageBytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pack)));
            await this.client.PostAsync(localhost, messageBytes); // Call must be awaited to avoid losing scope of the byte array.
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
                    this.server.Stop();
                    this.server.Dispose();
                    
                    this.upgradeLayer.WaitForExit();
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
