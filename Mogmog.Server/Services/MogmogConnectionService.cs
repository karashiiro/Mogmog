﻿using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Mogmog.Protos;
using Serilog;
using System;
using System.Collections.Generic;
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
        private readonly GameDataService _gameDataService;
        private readonly MogmogTransmissionService _transmitter;
        private readonly ServerFlags _flags;
        private readonly UserManagerService _userManager;

        private CancellationTokenSource _tokenSource;
        private IServerStreamWriter<ChatMessage> _responseStream;
        private User _currentUser;

        public MogmogConnectionService(GameDataService gameDataService, UserManagerService userManager, MogmogTransmissionService transmitter, IConfiguration config)

        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));
            _flags = ServerFlagsParser.ParseFromConfig(config);
            _transmitter.MessageSent += SendToClient;
        }

        public override Task<ChatServerInfo> GetChatServerInfo(ReqChatServerInfo req, ServerCallContext context)
        {
            return Task.FromResult(new ChatServerInfo { Flags = (int)_flags, ServerId = Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_ID") });
        }

        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            if (requestStream == null)
                throw new ArgumentNullException(nameof(requestStream));
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));

            if (context == null)
                throw new ArgumentNullException(nameof(context));
            _currentUser = await Authenticate(context.RequestHeaders);
            _currentUser.ForcedDisconnect += (sender, e) => this.Dispose();
            _userManager.AddUser(_currentUser);
            Log.Information("Added user {StreamName} to user list.", _currentUser.Name);
            
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

        public override async Task<UserInfo> GetUserInfo(ReqUserInfo req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var name = req.UserName;
            var worldId = req.UserWorldId;
            var user = _userManager.GetUser(name, worldId);
            if (user == null)
            {
                return new UserInfo
                {
                    Success = false,
                    Message = ResponseMessages.UserNotFound,
                    UserExternalName = string.Empty,
                    UserId = 0,
                };
            }
            var info = await user.GetUserInfo();
            return new UserInfo
            {
                Success = true,
                Message = string.Empty,
                UserExternalName = info.Username + "#" + info.Discriminator,
                UserId = info.Id,
            };
        }

        public override async Task<GeneralResult> OpUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.OpUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> OpDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.OpUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> BanUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.BanUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> BanDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.BanUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> UnbanUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.UnbanUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> UnbanDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.BanUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> TempbanUser(ReqTempbanUser req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.TempbanUser(name, worldId, DateTime.FromBinary(req.UnbanTimestamp));
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> TempbanDiscordUser(ReqTempbanDiscordUser req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.TempbanUser(user, DateTime.FromBinary(req.UnbanTimestamp));
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> KickUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.KickUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> KickDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.KickUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> MuteUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.MuteUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> MuteDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.MuteUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> UnmuteUser(UserActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.OAuth2Code);
            var result = isOp.Item2;
            if (isOp.Item1)
            {
                var name = req.UserName;
                var worldId = req.UserWorldId;
                result = await _userManager.UnmuteUser(name, worldId);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public override async Task<GeneralResult> UnmuteDiscordUser(UserDiscordActionRequest req, ServerCallContext context)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            var isOp = await _userManager.IsOp(req.StateKey);
            var result = isOp.Item2;
            if (result == MogmogOperationResult.Success)
            {
                var getUserResult = await _userManager.GetUser(req.Id);
                var user = getUserResult.Item1;
                result = getUserResult.Item2;
                if (result == MogmogOperationResult.Success)
                    result = await _userManager.UnmuteUser(user);
            }
            return new GeneralResult
            {
                Success = true,
                Message = result.ToString(),
            };
        }

        public void SendToClient(object sender, MessageEventArgs e)
        {
            if (e == null)
                return;
            _responseStream.WriteAsync(e.Message);
        }

        private async Task<User> Authenticate(Metadata headers)
        {
            var oAuth2CodeEntry = headers.FirstOrDefault((kvp) => kvp.Key == "code");
            if (oAuth2CodeEntry == null)
                throw new HttpRequestException(HttpStatusCodes.Unauthorized);
            var oAuth2Code = oAuth2CodeEntry.Value;
            AccessCodeResponse accessInfo = null;
            if (_flags.HasFlag(ServerFlags.RequiresDiscordOAuth2))
            {
                var specialUserToken = await UserManagerService.GetSpecialUserToken();
                if (string.IsNullOrEmpty(specialUserToken) || specialUserToken != oAuth2Code)
                {
                    var authInfo = await DiscordOAuth2.Authorize(oAuth2Code);
                    accessInfo = authInfo ?? throw new HttpRequestException(HttpStatusCodes.Unauthorized);
                }
                else
                {
                    accessInfo = new AccessCodeResponse { Bypass = true };
                }
            }
            var nameEntry = headers.FirstOrDefault((kvp) => kvp.Key == "name");
            var worldIdEntry = headers.FirstOrDefault((kvp) => kvp.Key == "worldId");
            if (nameEntry == null || worldIdEntry == null || !int.TryParse(worldIdEntry.Value, out int worldId))
                throw new HttpRequestException(HttpStatusCodes.BadRequest);
            var name = nameEntry.Value;
            try
            {
                _ = _gameDataService.Worlds[worldId];
            }
            catch (KeyNotFoundException)
            {
                throw new HttpRequestException(HttpStatusCodes.BadRequest);
            }
            var user = new User(name, worldId)
            {
                AuthState = new DiscordOAuth2()
                {
                    AccessInformation = accessInfo,
                },
            };
            return user;
        }

        private void Stop()
        {
            _tokenSource?.Cancel();
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
                    if (_currentUser != null)
                    {
                        _currentUser.ForcedDisconnect -= (sender, e) => this.Dispose();
                        _userManager.RemoveUser(_currentUser);
                    }
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
