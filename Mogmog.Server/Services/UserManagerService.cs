using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Server.Services
{
    public class TempbanEntry
    {
        public ulong UserId { get; set; }

        public string Name { get; set; }

        public int WorldId { get; set; }

        public DateTime EndTime { get; set; }
    }

    public enum MogmogOperationResult
    {
        Success,
        Failed,
        NoAuthentication,
    }

    public class UserManagerService : IDisposable
    {
        private readonly CancellationTokenSource _tokenSource;
        private readonly ConcurrentList<User> _userList;
        private readonly ConcurrentList<ulong> _opList;
        private readonly ConcurrentList<ulong> _bannedUserList;
        private readonly ConcurrentList<ulong> _mutedUserList;
        private readonly ConcurrentList<TempbanEntry> _tempbanList;

        public UserManagerService()
        {
            _userList = new ConcurrentList<User>();

            if (File.Exists("op_targetUsers"))
                _opList = JsonConvert.DeserializeObject<ConcurrentList<ulong>>(File.ReadAllText("op_targetUsers"));
            else
                _opList = new ConcurrentList<ulong>();

            if (File.Exists("banned_targetUsers"))
                _bannedUserList = JsonConvert.DeserializeObject<ConcurrentList<ulong>>(File.ReadAllText("banned_targetUsers"));
            else
                _bannedUserList = new ConcurrentList<ulong>();

            if (File.Exists("tempbanned_targetUsers"))
                _tempbanList = JsonConvert.DeserializeObject<ConcurrentList<TempbanEntry>>(File.ReadAllText("tempbanned_targetUsers"));
            else
                _tempbanList = new ConcurrentList<TempbanEntry>();

            if (File.Exists("muted_targetUsers"))
                _mutedUserList = JsonConvert.DeserializeObject<ConcurrentList<ulong>>(File.ReadAllText("muted_targetUsers"));
            else
                _mutedUserList = new ConcurrentList<ulong>();

            _tokenSource = new CancellationTokenSource();
            _ = CheckTempBans(_tokenSource.Token);
            _ = BackupLists(_tokenSource.Token);
        }

        public MogmogOperationResult AddUser(User targetUser)
        {
            var result = _userList.TryAdd(targetUser);
            if (result)
                return MogmogOperationResult.Success;
            return MogmogOperationResult.Failed;
        }

        public void RemoveUser(User targetUser)
            => _userList.Remove(targetUser, out _);

        public User GetUser(string name, int worldId)
            => _userList.FirstOrDefault((u) => u.Name == name && u.WorldId == worldId);

        public async Task<Tuple<User, MogmogOperationResult>> GetUser(ulong id)
        {
            var testUser = _userList.FirstOrDefault();
            if (testUser == null)
                return new Tuple<User, MogmogOperationResult>(null, MogmogOperationResult.Failed);
            if (testUser.AuthState == null)
                return new Tuple<User, MogmogOperationResult>(null, MogmogOperationResult.NoAuthentication);
            User result = null;
            foreach (var user in _userList)
            {
                if (await user.GetId() == id)
                    result = user;
            }
            if (result == null)
                return new Tuple<User, MogmogOperationResult>(null, MogmogOperationResult.Failed);
            return new Tuple<User, MogmogOperationResult>(result, MogmogOperationResult.Success);
        }

        public async Task<Tuple<User, MogmogOperationResult>> GetUser(string oAuth2Code)
        {
            if (string.IsNullOrEmpty(oAuth2Code))
                return new Tuple<User, MogmogOperationResult>(null, MogmogOperationResult.NoAuthentication);
            var res = await DiscordOAuth2.Authorize(oAuth2Code);
            var userObj = await DiscordOAuth2.GetUserInfo(res);
            var id = userObj.Id;
            return await GetUser(id);
        }

        public async Task<MogmogOperationResult> OpUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await OpUser(targetUser);
        }

        public async Task<MogmogOperationResult> OpUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            var result = _opList.TryAdd((await targetUser.GetId()).GetValueOrDefault());
            if (result)
                return MogmogOperationResult.Success;
            return MogmogOperationResult.Failed;
        }

        public bool IsOp(ulong targetUserId)
            => _opList.Contains(targetUserId);

        public async Task<Tuple<bool, MogmogOperationResult>> IsOp(string oAuth2Code)
        {
            var specialUserToken = await GetSpecialUserToken();
            if (string.IsNullOrEmpty(specialUserToken) || specialUserToken != oAuth2Code)
            {
                var user = await GetUser(oAuth2Code);
                if (user.Item1 == null)
                    return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.Failed);
                var result = await IsOp(user.Item1);
                return new Tuple<bool, MogmogOperationResult>(result, MogmogOperationResult.Success);
            }
            else
            {
                return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
            }
        }

        public async Task<bool> IsOp(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            return _opList.Contains((await targetUser.GetId()).GetValueOrDefault());
        }

        public async Task<MogmogOperationResult> KickUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await KickUser(targetUser);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "For API consistency")]
        public Task<MogmogOperationResult> KickUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            targetUser.Disconnect();
            return Task.FromResult(MogmogOperationResult.Success);
        }

        public async Task<MogmogOperationResult> MuteUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await MuteUser(targetUser);
        }

        public async Task<MogmogOperationResult> MuteUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            var result = _mutedUserList.TryAdd((await targetUser.GetId()).GetValueOrDefault());
            if (result)
                return MogmogOperationResult.Success;
            return MogmogOperationResult.Failed;
        }

        public async Task<MogmogOperationResult> UnmuteUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await UnmuteUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnmuteUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _mutedUserList.Remove((await targetUser.GetId()).GetValueOrDefault(), out _);
            return MogmogOperationResult.Success;
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await HasMutedUser(targetUser);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.NoAuthentication);
            return new Tuple<bool, MogmogOperationResult>(_mutedUserList.Contains((await targetUser.GetId()).GetValueOrDefault()), MogmogOperationResult.Success);
        }

        public async Task<MogmogOperationResult> BanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await BanUser(targetUser);
        }

        public async Task<MogmogOperationResult> BanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            var result = _bannedUserList.TryAdd((await targetUser.GetId()).GetValueOrDefault());
            if (result)
            {
                targetUser.Disconnect();
                return MogmogOperationResult.Success;
            }
            return MogmogOperationResult.Failed;
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await HasBannedUser(targetUser);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.NoAuthentication);
            return new Tuple<bool, MogmogOperationResult>(_bannedUserList.Contains((await targetUser.GetId()).GetValueOrDefault()), MogmogOperationResult.Success);
        }

        public async Task<MogmogOperationResult> UnbanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await UnbanUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnbanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _bannedUserList.Remove((await targetUser.GetId()).GetValueOrDefault(), out _);
            await UnTempbanUser(targetUser);
            return MogmogOperationResult.Success;
        }

        public async Task<MogmogOperationResult> TempbanUser(string name, int worldId, DateTime end)
        {
            var targetUser = GetUser(name, worldId);
            return await TempbanUser(targetUser, end);
        }

        public async Task<MogmogOperationResult> TempbanUser(User targetUser, DateTime end)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            await BanUser(targetUser);
            var result = _tempbanList.TryAdd(new TempbanEntry
            {
                UserId = (await targetUser.GetId()).GetValueOrDefault(),
                Name = targetUser.Name,
                WorldId = targetUser.WorldId,
                EndTime = end,
            });
            if (result)
                return MogmogOperationResult.Success;
            return MogmogOperationResult.Failed;
        }

        public async Task<MogmogOperationResult> UnTempbanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await UnTempbanUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnTempbanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var id = await targetUser.GetId();
            var tempbanEntry = _tempbanList.FirstOrDefault((entry) => entry.UserId == id.GetValueOrDefault());
            if (tempbanEntry != null)
                _tempbanList.Remove(tempbanEntry, out _);
            return MogmogOperationResult.Success;
        }

        private async Task CheckTempBans(CancellationToken token)
        {
            while (true)
            {
                foreach (var tempban in _tempbanList)
                {
                    if (token.IsCancellationRequested)
                        token.ThrowIfCancellationRequested();
                    if (DateTime.Now > tempban.EndTime)
                    {
                        _bannedUserList.Remove(tempban.UserId, out _);
                        _tempbanList.Remove(tempban, out _);
                    }
                }
                await Task.Delay(3000, token);
            }
        }

        private async Task BackupLists(CancellationToken token)
        {
            while (true)
            {
                await File.WriteAllTextAsync("op_targetUsers", JsonConvert.SerializeObject(_opList), token);
                await File.WriteAllTextAsync("banned_targetUsers", JsonConvert.SerializeObject(_bannedUserList), token);
                await File.WriteAllTextAsync("tempbanned_targetUsers", JsonConvert.SerializeObject(_tempbanList), token);
                await File.WriteAllTextAsync("muted_targetUsers", JsonConvert.SerializeObject(_mutedUserList), token);
                await Task.Delay(5000, token);
            }
        }

        public static async Task<string> GetSpecialUserToken()
        {
            if (Environment.GetEnvironmentVariable("MOGMOG_DISCORD_BOT_PATH") == null)
                return null;
            var specialUserTokenPath = Path.Combine(Environment.GetEnvironmentVariable("MOGMOG_DISCORD_BOT_PATH"), "identifier");
            if (File.Exists(specialUserTokenPath))
            {
                return await File.ReadAllTextAsync(specialUserTokenPath);
            }
            return null;
        }

        private static void ThrowIfUserNull(User targetUser)
        {
            if (targetUser == null)
                throw new ArgumentNullException(nameof(targetUser));
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                    foreach (var targetUser in _userList)
                    {
                        targetUser.Dispose();
                    }
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
