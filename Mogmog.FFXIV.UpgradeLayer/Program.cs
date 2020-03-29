using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    class Program
    {
        static MogmogConnectionManager connectionManager;

        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
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
