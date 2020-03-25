using Mogmog.Protos;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    class Program
    {
        static async void Main(string[] args)
        {
            var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            var connectionManager = new MogmogConnectionManager(config)
            {
                MessageReceivedDelegate = MessageReceived
            };
            await Task.Delay(-1);
        }

        static void MessageReceived(ChatMessage message, int channelId)
        {
            //
        }
    }
}
