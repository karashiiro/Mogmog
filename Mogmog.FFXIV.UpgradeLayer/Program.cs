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
                    try
                    {
                        var add = JsonConvert.DeserializeObject<AddHostInterop>(input);
                        connectionManager.AddHost(add.HostAdd);
                    }
                    catch
                    {
                        var remove = JsonConvert.DeserializeObject<RemoveHostInterop>(input);
                        connectionManager.RemoveHost(remove.HostRemove);
                    }
                }
            }
        }

        static void MessageReceived(ChatMessage message, int channelId)
        {
            // Send the message back to the caller.
            Console.WriteLine($"({message.Author}) {message.Content}");
        }
    }

    struct ChatMessageInterop
    {
        public ChatMessage Message;
        public int ChannelId;
    }

    struct AddHostInterop
    {
        public string HostAdd;
    }

    struct RemoveHostInterop
    {
        public string HostRemove;
    }
}
