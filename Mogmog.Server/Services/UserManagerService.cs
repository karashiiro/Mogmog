using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Server.Services
{
    public class TempbanEntry
    {
        public string Id { get; set; }

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
        private readonly ConcurrentList<UserFragment> _opList;
        private readonly ConcurrentList<UserFragment> _bannedUserList;
        private readonly ConcurrentList<UserFragment> _mutedUserList;
        private readonly ConcurrentList<TempbanEntry> _tempbanList;

        public UserManagerService()
        {
            _userList = new ConcurrentList<User>();

            _opList = File.Exists("op_targetUsers")
                ? JsonConvert.DeserializeObject<ConcurrentList<UserFragment>>(File.ReadAllText("op_targetUsers"))
                : new ConcurrentList<UserFragment>();
            _bannedUserList = File.Exists("banned_targetUsers")
                ? JsonConvert.DeserializeObject<ConcurrentList<UserFragment>>(File.ReadAllText("banned_targetUsers"))
                : new ConcurrentList<UserFragment>();
            _tempbanList = File.Exists("tempbanned_targetUsers")
                ? JsonConvert.DeserializeObject<ConcurrentList<TempbanEntry>>(File.ReadAllText("tempbanned_targetUsers"))
                : new ConcurrentList<TempbanEntry>();
            _mutedUserList = File.Exists("muted_targetUsers")
                ? JsonConvert.DeserializeObject<ConcurrentList<UserFragment>>(File.ReadAllText("muted_targetUsers"))
                : new ConcurrentList<UserFragment>();

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
                if (await user.GetId() == id)
                    result = user;
            return new Tuple<User, MogmogOperationResult>(result, result == null ? MogmogOperationResult.Failed : MogmogOperationResult.Success);
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

        public Task<MogmogOperationResult> OpUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return OpUser(targetUser);
        }

        public async Task<MogmogOperationResult> OpUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = _opList.TryAdd(targetUser.AuthState != null
                ? new UserFragment { Id = (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture) }
                : new UserFragment { Name = targetUser.Name, WorldId = targetUser.WorldId });
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public bool IsOp(string name, int worldId)
            => _opList.FirstOrDefault(user => user.Name == name && user.WorldId == worldId) != null;

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
            return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
        }

        public async Task<bool> IsOp(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            foreach (var user in _opList)
                if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                    return true;
            return false;
        }

        public async Task<MogmogOperationResult> KickUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return await KickUser(targetUser);
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "For API consistency")]
        public Task<MogmogOperationResult> KickUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            targetUser.Disconnect();
            return Task.FromResult(MogmogOperationResult.Success);
        }

        public Task<MogmogOperationResult> MuteUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return MuteUser(targetUser);
        }

        public async Task<MogmogOperationResult> MuteUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = _mutedUserList.TryAdd(targetUser.AuthState != null
                ? new UserFragment { Id = (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture) }
                : new UserFragment { Name = targetUser.Name, WorldId = targetUser.WorldId });
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public Task<MogmogOperationResult> UnmuteUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return UnmuteUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnmuteUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = false;
            if (targetUser.AuthState != null)
            {
                foreach (var user in _mutedUserList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                        result = _mutedUserList.Remove(user, out _);
            }
            else
            {
                foreach (var user in _mutedUserList)
                    if (user.Name == targetUser.Name && user.WorldId == targetUser.WorldId)
                        result = _mutedUserList.Remove(user, out _);
            }
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return HasMutedUser(targetUser);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState != null)
            {
                foreach (var user in _mutedUserList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                        return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
            }
            else
            if (_mutedUserList.Any(user => user.Name == targetUser.Name && user.WorldId == targetUser.WorldId))
                return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
            return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.Success);
        }

        public Task<MogmogOperationResult> BanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return BanUser(targetUser);
        }

        public async Task<MogmogOperationResult> BanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = false;
            if (targetUser.AuthState != null)
            {
                foreach (var user in _bannedUserList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                    {
                        result = _bannedUserList.Remove(user, out _);
                        result = result && await KickUser(targetUser) == MogmogOperationResult.Success;
                    }
            }
            else
            {
                foreach (var user in _bannedUserList)
                    if (user.Name == targetUser.Name && user.WorldId == targetUser.WorldId)
                    {
                        result = _bannedUserList.Remove(user, out _);
                        result = result && await KickUser(targetUser) == MogmogOperationResult.Success;
                    }
            }
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return HasBannedUser(targetUser);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            if (targetUser.AuthState != null)
            {
                foreach (var user in _bannedUserList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                        return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
            }
            else
                if (_bannedUserList.Any(user => user.Name == targetUser.Name && user.WorldId == targetUser.WorldId))
                    return new Tuple<bool, MogmogOperationResult>(true, MogmogOperationResult.Success);
            return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.Success);
        }

        public Task<MogmogOperationResult> UnbanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return UnbanUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnbanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = false;
            if (targetUser.AuthState != null)
            {
                foreach (var user in _bannedUserList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                    {
                        result = _bannedUserList.Remove(user, out _);
                        result = result && await UnTempbanUser(targetUser) == MogmogOperationResult.Success;
                    }
            }
            else
            {
                foreach (var user in _bannedUserList)
                    if (user.Name == targetUser.Name && user.WorldId == targetUser.WorldId)
                    {
                        result = _bannedUserList.Remove(user, out _);
                        result = result && await UnTempbanUser(targetUser) == MogmogOperationResult.Success;
                    }
            }
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public Task<MogmogOperationResult> TempbanUser(string name, int worldId, DateTime end)
        {
            var targetUser = GetUser(name, worldId);
            return TempbanUser(targetUser, end);
        }

        public async Task<MogmogOperationResult> TempbanUser(User targetUser, DateTime end)
        {
            ThrowIfUserNull(targetUser);
            await BanUser(targetUser);
            var result = _tempbanList.TryAdd(new TempbanEntry
            {
                Id = (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                Name = targetUser.Name,
                WorldId = targetUser.WorldId,
                EndTime = end,
            });
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
        }

        public Task<MogmogOperationResult> UnTempbanUser(string name, int worldId)
        {
            var targetUser = GetUser(name, worldId);
            return UnTempbanUser(targetUser);
        }

        public async Task<MogmogOperationResult> UnTempbanUser(User targetUser)
        {
            ThrowIfUserNull(targetUser);
            var result = false;
            if (targetUser.AuthState != null)
            {
                foreach (var user in _tempbanList)
                    if (user.Id == (await targetUser.GetId()).GetValueOrDefault().ToString(CultureInfo.InvariantCulture))
                        result = _tempbanList.Remove(user, out _);
            }
            else
            {
                foreach (var user in _tempbanList)
                    if (user.Name == targetUser.Name && user.WorldId == targetUser.WorldId)
                        result = _tempbanList.Remove(user, out _);
            }
            return result ? MogmogOperationResult.Success : MogmogOperationResult.Failed;
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
                        await UnbanUser(tempban.Name, tempban.WorldId);
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
