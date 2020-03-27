using Grpc.Core;
using Mogmog.Protos;
using Serilog;
using System;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Server.Services
{
    public class MogmogConnectionService : ChatServiceBase, IDisposable
    {
        private readonly GameDataService _gameDataService;
        private readonly MogmogTransmissionService _transmitter;

        private IServerStreamWriter<ChatMessage> _responseStream;

        private bool _taskActive;

        public MogmogConnectionService(GameDataService gameDataService, MogmogTransmissionService transmitter)
        {
            _gameDataService = gameDataService;
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));

            _transmitter.MessageSent += SendToClient;
            _taskActive = true;
        }

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            
            Log.Information("Added stream {StreamName} to client list.", requestStream.ToString());
            while (_taskActive)
            {
                if (!await requestStream.MoveNext())
                    continue;
                var nextMessage = requestStream.Current;
                nextMessage.World = _gameDataService.Worlds[nextMessage.WorldId];
                Log.Information("({Author}) {Content}", nextMessage.Author, nextMessage.Content);
                _transmitter.Send(nextMessage);
            }
        }

        public void SendToClient(object sender, MessageEventArgs e)
        {
            if (e == null)
                return;
            _responseStream.WriteAsync(e.Message);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _taskActive = false;
                    _transmitter.MessageSent -= SendToClient;
                }

                disposedValue = true;
            }
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            Dispose(true);
        }
        #endregion
    }
}
