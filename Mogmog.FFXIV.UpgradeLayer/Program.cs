using Mogmog.Protos;
using Newtonsoft.Json;
using PeanutButter.SimpleHTTPServer;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    class Program
    {
        static HttpClient client;
        static HttpServer server;
        static MogmogConnectionManager connectionManager;
        static Uri localhost;

        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            client = new HttpClient();
            server = new HttpServer(int.Parse(args[1]) + 1, true, (line) => {});

            localhost = new Uri($"http://localhost:{args[1]}");

            server.AddJsonDocumentHandler(ReadInput);

            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            connectionManager = new MogmogConnectionManager(config)
            {
                MessageReceivedDelegate = MessageReceived
            };

            await Task.Delay(-1);
        }

        static byte[] ReadInput(HttpProcessor processor, Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            string input = BitConverter.ToString(memoryStream.GetBuffer());

            Console.WriteLine(input);

            try
            {
                var message = JsonConvert.DeserializeObject<ChatMessageInterop>(input);
                connectionManager.MessageSend(message.Message, message.ChannelId);
            }
            catch
            {
                var genericInterop = JsonConvert.DeserializeObject<GenericInterop>(input);
                switch (genericInterop.Command)
                {
                    case "AddHost":
                        connectionManager.AddHost(genericInterop.Arg);
                        break;
                    case "RemoveHost":
                        connectionManager.RemoveHost(genericInterop.Arg);
                        break;
                }
            }

            return new byte[0];
        }

        static void MessageReceived(ChatMessage message, int channelId)
        {
            var interopMessage = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            client.PostAsync(localhost, new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(interopMessage))));
            Console.WriteLine("Responded.");
        }
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
