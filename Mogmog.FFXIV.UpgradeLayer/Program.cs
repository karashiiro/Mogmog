using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    class Program
    {
        static MogmogConnectionManager connectionManager;

        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            var hwnd = Process.GetCurrentProcess().MainWindowHandle;
            //ShowWindow(hwnd, 0); // Hides the console window.
            
            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            connectionManager = new MogmogConnectionManager(config)
            {
                MessageReceivedDelegate = MessageReceived
            };

            await ReadInput();
        }

        static Task ReadInput()
        {
            while (true)
            {
                string input = Console.ReadLine();
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
                        case "ShowWindow":
                            ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 5);
                            break;
                        case "HideWindow":
                            ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 0);
                            break;
                    }
                }
            }
        }

        static void MessageReceived(ChatMessage message, int channelId)
        {
            var interopMessage = new ChatMessageInterop
            {
                Message = message,
                ChannelId = channelId,
            };
            Console.WriteLine(JsonConvert.SerializeObject(interopMessage));
        }

        [DllImport("user32")]
        static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
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
