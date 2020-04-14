using Mogmog.Events;
using Mogmog.Logging;
using Mogmog.Protos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PeanutButter.SimpleHTTPServer;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    static class Program
    {
        static HttpClient client;
        static IConnectionManager connectionManager;
        static Uri localhost;

        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            Mogger.Logger = new ProgramLogger();

            var traceListener = new ProgramTraceListener();
            Trace.Listeners.Add(traceListener);

            client = new HttpClient();
            #if !DEBUGSTANDALONE
            using var server = new HttpServer(int.Parse(args[1], CultureInfo.InvariantCulture) + 1, true, (line) =>
            {
                #if DEBUG
                Console.WriteLine(line);
                #endif
            });

            localhost = new Uri($"http://localhost:{args[1]}");

            server.AddJsonDocumentHandler((processor, stream) => ReadInput(stream));

            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            #else
            var config = new MogmogConfiguration();
            #endif
            connectionManager = new MogmogConnectionManager(config);

            #if DEBUGSTANDALONE
            await Task.Run(() =>
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    var args = input.Split(' ');
                    var command = args[0].ToLowerInvariant();
                    args = args[1..];
                    switch (command)
                    {
                        case "addhost":
                            connectionManager.AddHost(args[0]);
                            break;
                        case "removehost":
                            connectionManager.RemoveHost(args[0]);
                            break;
                        case "reloadhost":
                            connectionManager.ReloadHost(args[0]);
                            break;
                        case "send":
                            var message = new ChatMessage
                            {
                                Author = "Test User",
                                Content = string.Join(' ', args[1..]),
                            };
                            var channelId = int.Parse(args[0], CultureInfo.InvariantCulture);
                            connectionManager.MessageSend(message, channelId);
                            break;
                        default:
                            Console.WriteLine("Command not recognized.");
                            break;
                    }
                }
            });
            #else
            connectionManager.MessageReceivedEvent += MessageReceived;

            await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(10000);
                    if (Process.GetProcessesByName("ffxiv_dx11").Length == 0)
                        Environment.Exit(-1);
                }
            });
            #endif
        }

        static byte[] ReadInput(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            string input = Encoding.UTF8.GetString(memoryStream.GetBuffer());

            #if DEBUG || DEBUGSTANDALONE
            Console.WriteLine(input);
            #endif

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
                    case "ReloadHost":
                        connectionManager.ReloadHost(genericInterop.Arg);
                        break;
                    default:
                        throw new NotSupportedException();
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
            #if DEBUG || DEBUGSTANDALONE
            Console.WriteLine($"Making request to {localhost.AbsoluteUri}:\n({message.Author} * {message.World}) {message.Content}");
            #endif
            await SendToParent(interopMessage);
        }

        public static async Task SendToParent(object obj)
        {
            using var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj)));
            await client.PostAsync(localhost, bytes);
        }
    }
}
