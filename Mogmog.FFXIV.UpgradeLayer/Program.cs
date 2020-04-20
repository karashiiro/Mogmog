using Mogmog.Events;
using Mogmog.Logging;
using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using SimpleIPCHttp;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public static class Program
    {
        private static HttpClient client;
        private static IConnectionManager connectionManager;
        private static IpcInterface ipc;

        public static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        public static async Task MainAsync(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            Mogger.Logger = new ProgramLogger();

            var traceListener = new ProgramTraceListener();
            Trace.Listeners.Add(traceListener);

            #if !DEBUGSTANDALONE
            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            #else
            var config = new MogmogConfiguration();
            #endif
            connectionManager = new MogmogConnectionManager(config);
            connectionManager.MessageReceivedEvent += GrpcMessageReceived;

            var thisPort = int.Parse(args[^2], CultureInfo.InvariantCulture);
            var parentPort = int.Parse(args[^1], CultureInfo.InvariantCulture);
            client = new HttpClient();
            ipc = new IpcInterface(client, thisPort, parentPort);
            ipc.On<ChatMessageInterop>(ChatMessageReceived);
            ipc.On<GenericInterop>(CommandReceived);

            #if DEBUGSTANDALONE
            await Task.Run(() =>
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    if (string.IsNullOrEmpty(input))
                        continue;
                    var args = input.Split(' ');
                    var command = args[0].ToLowerInvariant();
                    args = args[1..];
                    switch (command)
                    {
                        case "addhost":
                            connectionManager.AddHost(args[0], false);
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
                            Console.WriteLine($"[GL{channelId}]<Test User>{string.Join(' ', args[1..])}");
                            connectionManager.MessageSend(message, channelId - 1);
                            break;
                        default:
                            Console.WriteLine("Command not recognized.");
                            break;
                    }
                }
            });
            #else
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

        private static void ChatMessageReceived(ChatMessageInterop chatMessage)
            => connectionManager.MessageSend(chatMessage.Message, chatMessage.ChannelId);

        private static void CommandReceived(GenericInterop genericInterop)
        {
            var args = genericInterop.Arg.Split(' ');
            switch (Enum.Parse<ClientOpcode>(genericInterop.Command))
            {
                case ClientOpcode.AddHost:
                    connectionManager.AddHost(args[0], bool.Parse(args[1]));
                    break;
                case ClientOpcode.RemoveHost:
                    connectionManager.RemoveHost(genericInterop.Arg);
                    break;
                case ClientOpcode.ReloadHost:
                    connectionManager.ReloadHost(genericInterop.Arg);
                    break;
                case ClientOpcode.BanUser:
                    connectionManager.BanUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], int.Parse(args[3], CultureInfo.CurrentCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
                    break;
                case ClientOpcode.UnbanUser:
                    connectionManager.UnbanUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], int.Parse(args[3], CultureInfo.CurrentCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
                    break;
                case ClientOpcode.TempbanUser:
                    connectionManager.TempbanUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), DateTime.FromBinary(long.Parse(args[2], CultureInfo.InvariantCulture)), args[3], int.Parse(args[4], CultureInfo.InvariantCulture), int.Parse(args[5], CultureInfo.InvariantCulture));
                    break;
                case ClientOpcode.KickUser:
                    connectionManager.KickUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], int.Parse(args[3], CultureInfo.CurrentCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
                    break;
                case ClientOpcode.MuteUser:
                    connectionManager.MuteUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], int.Parse(args[3], CultureInfo.CurrentCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
                    break;
                case ClientOpcode.UnmuteUser:
                    connectionManager.UnmuteUser(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], int.Parse(args[3], CultureInfo.CurrentCulture), int.Parse(args[4], CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
        
        private static void GrpcMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            _ = GrpcMessageReceivedAsync(e.Message, e.ChannelId);
        }

        private static Task GrpcMessageReceivedAsync(ChatMessage message, int channelId)
        {
            var interopMessage = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            #if DEBUG
            Console.WriteLine($"Making request to {ipc.PartnerAddress.AbsoluteUri}:\n({message.Author} * {message.World}) {message.Content}");
            #endif
            return SendToParent(interopMessage);
        }

        public static Task SendToParent<T>(T obj)
        {
            return ipc.SendMessage(obj);
        }
    }
}
