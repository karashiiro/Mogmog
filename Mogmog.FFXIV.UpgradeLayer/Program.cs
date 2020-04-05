using Mogmog.Protos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PeanutButter.SimpleHTTPServer;
using System;
using System.Globalization;
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
            server = new HttpServer(int.Parse(args[1], CultureInfo.InvariantCulture) + 1, true, (line) => { Console.WriteLine(line); });

            localhost = new Uri($"http://localhost:{args[1]}");

            server.AddJsonDocumentHandler(ReadInput);

            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            connectionManager = new MogmogConnectionManager(config);
            connectionManager.MessageReceivedEvent += MessageReceived;

            await Task.Delay(-1);
        }

        static byte[] ReadInput(HttpProcessor processor, Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            string input = Encoding.UTF8.GetString(memoryStream.GetBuffer());

            JToken message = JObject.Parse(input);
            if (message["Message"] != null) // Jank but whatever, ripping all this out once Dalamud on .NET Core is released
            {
                var chatMessage = message.ToObject<ChatMessageInterop>();
                connectionManager.MessageSend(chatMessage.Message, chatMessage.ChannelId);
            }
            else
            {
                var genericInterop = message.ToObject<GenericInterop>();
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

            return Array.Empty<byte>();
        }

        static void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            _ = MessageReceivedAsync(e.Message, e.ChannelId);
        }

        static async Task MessageReceivedAsync(ChatMessage message, int channelId)
        {
            var interopMessage = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            Console.WriteLine($"Making request to {localhost.AbsoluteUri}:\n({message.Author} * {message.World}) {message.Content}");
            using var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(interopMessage)));
            await client.PostAsync(localhost, bytes);
        }
    }

    struct ChatMessageInterop
    {
        public ChatMessage Message;
        public int ChannelId;
    }

#pragma warning disable CS0649
    struct GenericInterop
    {
        public string Command;
        public string Arg;
    }
#pragma warning restore CS0649
}
