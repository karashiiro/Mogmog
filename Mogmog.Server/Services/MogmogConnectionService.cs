using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Mogmog.Protos;
using Serilog;
using System;
using System.IO;
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

        private readonly ServerFlags _flags;

        public MogmogConnectionService(GameDataService gameDataService, MogmogTransmissionService transmitter, IConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            _discordOAuth2 = new DiscordOAuth2();
            _gameDataService = gameDataService;
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));
            _flags = ServerFlagsParser.ParseFromConfig(config);
            _transmitter.MessageSent += SendToClient;
        }

        public override Task<ChatServerInfo> GetChatServerInfo(ReqChatServerInfo req, ServerCallContext context)
        {
            return Task.FromResult(new ChatServerInfo { Flags = (int)_flags, ServerId = Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_ID") });
        }

        public override async Task<GeneralAck> SendOAuth2Code(ReqOAuth2Code code, ServerCallContext context)
        {
            var oAuth2Code = code?.OAuth2Code;
            if (oAuth2Code == null)
                throw new HttpRequestException(HttpStatusCodes.Unauthorized);
            var specialUserToken = await GetSpecialUserToken();
            if (string.IsNullOrEmpty(specialUserToken) || specialUserToken != oAuth2Code)
            {
                var authInfo = await DiscordOAuth2.Authorize(oAuth2Code);
                _discordOAuth2.AccessToken = authInfo ?? throw new HttpRequestException(HttpStatusCodes.Unauthorized);
            }
            else
            {
                _discordOAuth2.AccessToken = new AccessCodeResponse { Bypass = true };
            }
            return new GeneralAck();
        }

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));

            if (_flags.HasFlag(ServerFlags.RequiresDiscordOAuth2) && _discordOAuth2.AccessToken == null)
                throw new HttpRequestException(HttpStatusCodes.Unauthorized);
            
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
            _tokenSource?.Cancel();
        }

        public void SendToClient(object sender, MessageEventArgs e)
        {
            if (e == null)
                return;
            _responseStream.WriteAsync(e.Message);
        }

        private static async Task<string> GetSpecialUserToken()
        {
            if (Environment.GetEnvironmentVariable("DISCORD_BOT_PATH") == null)
                return null;
            var specialUserTokenPath = Path.Combine(Environment.GetEnvironmentVariable("DISCORD_BOT_PATH"), "identifier");
            if (File.Exists(specialUserTokenPath))
            {
                return await File.ReadAllTextAsync(specialUserTokenPath);
            }
            return null;
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
                    _tokenSource?.Dispose();
                    _transmitter.MessageSent -= SendToClient;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
