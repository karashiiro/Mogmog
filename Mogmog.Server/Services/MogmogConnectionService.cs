using Grpc.Core;
using Mogmog.Protos;
using Serilog;
using System;
using System.IO;
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

        private CancellationTokenSource _tokenSource;

        public MogmogConnectionService(GameDataService gameDataService, MogmogTransmissionService transmitter)
        {
            _gameDataService = gameDataService;
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));

            _transmitter.MessageSent += SendToClient;
        }

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            
            Log.Information("Added stream {StreamName} to client list.", requestStream.ToString());
            
            _tokenSource = new CancellationTokenSource();
            await ChatLoop(requestStream, _tokenSource.Token);
        }

        private async Task ChatLoop(IAsyncStreamReader<ChatMessage> requestStream, CancellationToken cancellationToken)
        {
            while (true)
            {
                bool ready;
                try
                {
                    ready = await requestStream.MoveNext(cancellationToken);
                }
                catch (IOException)
                {
                    Log.Error("The request stream was forcibly closed by the remote host.");
                    return;
                }
                if (!ready)
                    continue;
                var nextMessage = requestStream.Current;
                nextMessage.World = _gameDataService.Worlds[nextMessage.WorldId];
                Log.Information("({Author}) {Content}", nextMessage.Author, nextMessage.Content);
                _transmitter.Send(nextMessage);
            }
        }

        private void Stop()
        {
            _tokenSource.Cancel();
        }

        public void SendToClient(object sender, MessageEventArgs e)
        {
            if (e == null)
                return;
            _responseStream.WriteAsync(e.Message);
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    _tokenSource.Dispose();
                    _transmitter.MessageSent -= SendToClient;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
