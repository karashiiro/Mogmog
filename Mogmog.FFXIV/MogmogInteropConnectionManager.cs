using Mogmog.Protos;
using Newtonsoft.Json;
using PeanutButter.SimpleHTTPServer;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace Mogmog.FFXIV
{
    public class MogmogInteropConnectionManager : IDisposable
    {
        public delegate void MessageReceivedCallback(ChatMessage message, int channelId);
        public MessageReceivedCallback MessageReceivedDelegate;

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

            this.localhost = new Uri($"http://localhost:{this.server.Port}");

            this.config = config;

            var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Mogmog.FFXIV.UpgradeLayer.exe");
            var serializedConfig = JsonConvert.SerializeObject(this.config).Replace("\"", "\\\"");
            var args = new string[] { serializedConfig, this.server.Port.ToString() };
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
            var pack = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            SendToUpgradeLayer(JsonConvert.SerializeObject(pack));
        }

        public void AddHost(string hostname)
        {
            var pack = new GenericInterop
            {
                Command = "AddHost",
                Arg = hostname,
            };
            SendToUpgradeLayer(JsonConvert.SerializeObject(pack));
        }

        public void RemoveHost(string hostname)
        {
            var pack = new GenericInterop
            {
                Command = "RemoveHost",
                Arg = hostname,
            };
            SendToUpgradeLayer(JsonConvert.SerializeObject(pack));
        }

        private byte[] UpgradeLayerMessageReceived(HttpProcessor processor, Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                var messageInterop = JsonConvert.DeserializeObject<ChatMessageInterop>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));
                var message = messageInterop.Message;
                var channelId = messageInterop.ChannelId;
                MessageReceivedDelegate(message, channelId);
                return new byte[0];
            }
        }

        private void SendToUpgradeLayer(string message)
        {
            this.client.PostAsync(localhost, new ByteArrayContent(Encoding.UTF8.GetBytes(message)));
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

                    this.server.Stop();
                    this.server.Dispose();
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
