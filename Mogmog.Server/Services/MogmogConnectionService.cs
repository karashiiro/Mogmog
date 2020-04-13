using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Mogmog.Protos;
using Serilog;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.Server.Services
{
    public class MogmogConnectionService : ChatServiceBase, IDisposable
    {
        private readonly DiscordOAuth2 _discordOAuth2;
        private readonly GameDataService _gameDataService;
        private readonly MogmogTransmissionService _transmitter;

        private IServerStreamWriter<ChatMessage> _responseStream;

        private CancellationTokenSource _tokenSource;

        private readonly BitVector32 _flags;
        private string _authToken;

        public MogmogConnectionService(GameDataService gameDataService, MogmogTransmissionService transmitter, IConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            _discordOAuth2 = new DiscordOAuth2();
            _gameDataService = gameDataService;
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));
            _flags = new BitVector32(int.Parse(config["Flags"], CultureInfo.InvariantCulture));
            _transmitter.MessageSent += SendToClient;
        }

        public override Task<ChatServerFlags> GetChatServerFlags(ReqChatServerFlags req, ServerCallContext context)
        {
            return Task.FromResult(new ChatServerFlags { Flags = _flags.Data });
        }

        public override async Task<GeneralAck> SendOAuth2Code(ReqOAuth2Code code, ServerCallContext context)
        {
            var oAuthCode = code?.OAuth2Code;
            if (oAuthCode == null)
                throw new HttpRequestException("401 Unauthorized");
            var authInfo = await DiscordOAuth2.Authorize(oAuthCode);
            _discordOAuth2.AccessToken = authInfo ?? throw new HttpRequestException("401 Unauthorized");
            _authToken = authInfo.AccessToken;
            return new GeneralAck();
        }

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));

            if (_flags[0] && _discordOAuth2.AccessToken == null)
                throw new HttpRequestException("401 Unauthorized");
            
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
