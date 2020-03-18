using Mogmog.Models;
using System.Threading.Tasks;

namespace Mogmog.Services
{
    public class ChatService
    {
        private readonly GameDataService _gameDataService;

        public ChatService(GameDataService gameDataService)
        {
            _gameDataService = gameDataService;
        }

        public async Task MessageRecieved(Message message)
        {
            message.World = _gameDataService.Worlds[message.WorldId];
            await Task.Delay(1);
        }
    }
}
