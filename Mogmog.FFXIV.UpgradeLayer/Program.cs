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
            var traceListener = new CallbackTraceListener();
            traceListener.LogEvent += Log;
            Trace.Listeners.Add(traceListener);

            client = new HttpClient();
            using var server = new HttpServer(int.Parse(args[1], CultureInfo.InvariantCulture) + 1, true, (line) =>
            {
                #if DEBUG
                Console.WriteLine(line);
                #endif
            });

            localhost = new Uri($"http://localhost:{args[1]}");

            server.AddJsonDocumentHandler((processor, stream) => ReadInput(stream));

            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            connectionManager = new MogmogConnectionManager(config);
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
        }

        static byte[] ReadInput(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            string input = Encoding.UTF8.GetString(memoryStream.GetBuffer());

            #if DEBUG
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

        static void Log(object sender, LogEventArgs e)
        {
            _ = LogAsync(e.LogMessage, e.IsError);
        }

        static async Task MessageReceivedAsync(ChatMessage message, int channelId)
        {
            var interopMessage = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            #if DEBUG
            Console.WriteLine($"Making request to {localhost.AbsoluteUri}:\n({message.Author} * {message.World}) {message.Content}");
            #endif
            using var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(interopMessage)));
            await client.PostAsync(localhost, bytes);
        }

        static async Task LogAsync(string logMessage, bool isError)
        {
            var interopLog = new GenericInterop
            {
                Command = logMessage,
                Arg = isError.ToString(CultureInfo.InvariantCulture),
            };
            #if DEBUG
            Console.WriteLine($"Making request to {localhost.AbsoluteUri}:\n({(isError ? "ERROR" : "INFO")}) {logMessage}");
            #endif
            using var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(interopLog)));
            await client.PostAsync(localhost, bytes);
        }
    }
}
