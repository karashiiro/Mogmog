using Mogmog.Protos;
using Newtonsoft.Json;
using PeanutButter.SimpleHTTPServer;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }
    }

    public class MogmogInteropConnectionManager : IDisposable
    {
        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event MessageReceivedEventHandler MessageReceivedEvent;

        private readonly HttpClient client;
        private readonly HttpServer server;
        private readonly MogmogConfiguration config;
        private readonly Process upgradeLayer;
        private readonly Uri localhost;

        public MogmogInteropConnectionManager(MogmogConfiguration config, HttpClient client)
        {
            this.client = client;
            this.server = new HttpServer();

            this.server.AddJsonDocumentHandler(UpgradeLayerMessageReceived);

            this.localhost = new Uri($"http://localhost:{this.server.Port + 1}");

            this.config = config;

            var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe");
            var serializedConfig = JsonConvert.SerializeObject(this.config).Replace("\"", "\\\"");
            var args = new string[] { serializedConfig, this.server.Port.ToString(CultureInfo.InvariantCulture) };
            // Something about this makes the child process crash when the game closes if the plugin isn't disposed of properly, which is neat.
            var startInfo = new ProcessStartInfo(filePath, string.Join(" ", args))
            {
                //CreateNoWindow = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            };
            this.upgradeLayer = Process.Start(startInfo);
        }

        public void MessageSend(ChatMessage message, int channelId)
        {
            _ = SendToUpgradeLayer(message, channelId);
        }

        public void AddHost(string hostname)
        {
            _ = SendToUpgradeLayer("AddHost", hostname);
        }

        public void RemoveHost(string hostname)
        {
            _ = SendToUpgradeLayer("RemoveHost", hostname);
        }

        #region Interop Interface Methods
        private byte[] UpgradeLayerMessageReceived(HttpProcessor processor, Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var messageInterop = JsonConvert.DeserializeObject<ChatMessageInterop>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));
            var message = messageInterop.Message;
            var channelId = messageInterop.ChannelId;

            MessageReceivedEvent(this, new MessageReceivedEventArgs { Message = message, ChannelId = channelId });

            return Array.Empty<byte>();
        }

        private async Task SendToUpgradeLayer(string command, string arg)
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
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.server.Stop();
                    this.server.Dispose();
                    
                    this.upgradeLayer.WaitForExit();
                }

                disposedValue = true;
            }
        }

        [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Class does not need to free unmanaged resources.")]
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
