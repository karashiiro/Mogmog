using Grpc.Core;
using Mogmog.Protos;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client = Grpc.Core.IServerStreamWriter<Mogmog.Protos.ChatMessage>;
using static Mogmog.Protos.ChatService;
using System;

namespace Mogmog.Server.Services
{
    public class ChatService : ChatServiceBase
    {
        private readonly GameDataService _gameDataService;

        private readonly IList<Client> _clients;
        private readonly Queue<ChatMessage> _messageQueue;

        private readonly Task _runningTask;

        public ChatService(GameDataService gameDataService)
        {
            _gameDataService = gameDataService;

            _clients = new List<Client>();
            _messageQueue = new Queue<ChatMessage>();

            _runningTask = SendMessagesToAll();
        }

        public bool IsActive() => _runningTask.Status == TaskStatus.Running;

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            _clients.Add(responseStream);
            while (await requestStream.MoveNext())
            {
                var nextMessage = requestStream.Current;
                nextMessage.World = _gameDataService.Worlds[nextMessage.WorldId];
                Console.WriteLine(nextMessage.Author + " " + nextMessage.Content);
                _messageQueue.Enqueue(nextMessage);
            }
        }

        private async Task SendMessagesToAll()
        {
            while (true)
            {
                if (_messageQueue.Count == 0)
                    continue;
                var nextMessage = _messageQueue.Dequeue();
                foreach (Client client in _clients)
                {
                    await client.WriteAsync(nextMessage);
                }
            }
        }
    }
}
