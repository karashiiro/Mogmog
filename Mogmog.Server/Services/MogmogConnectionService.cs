using Grpc.Core;
using Mogmog.Protos;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Server.Services
{
    public class MogmogConnectionService : ChatServiceBase, IDisposable
    {
        private readonly GameDataService _gameDataService;
        private readonly MogmogTransmissionService _transmitter;

        private IServerStreamWriter<ChatMessage> _responseStream;

        private Task _runningTask;
        private CancellationTokenSource _tokenSource;

        public MogmogConnectionService(GameDataService gameDataService, MogmogTransmissionService transmitter)
        {
            _gameDataService = gameDataService;
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));

            _transmitter.MessageSent += SendToClient;
        }

        public override Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            
            Log.Information("Added stream {StreamName} to client list.", requestStream.ToString());
            
            _tokenSource = new CancellationTokenSource();
            _runningTask = Task.WhenAny(ChatLoop(requestStream), Task.Run(() =>
            {
                while (true)
                {
                    _tokenSource.Token.ThrowIfCancellationRequested();
                }
            }));
            
            return Task.CompletedTask;
        }

        private async Task ChatLoop(IAsyncStreamReader<ChatMessage> requestStream)
        {
            while (true)
            {
                if (!await requestStream.MoveNext())
                    continue;
                var nextMessage = requestStream.Current;
                nextMessage.World = _gameDataService.Worlds[nextMessage.WorldId];
                Log.Information("({Author}) {Content}", nextMessage.Author, nextMessage.Content);
                _transmitter.Send(nextMessage);
            }
        }

        private async Task Stop()
        {
            _tokenSource.Cancel();
            await _runningTask;
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
                    Stop().Wait();
                    _runningTask.Dispose();
                    _tokenSource.Dispose();
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
