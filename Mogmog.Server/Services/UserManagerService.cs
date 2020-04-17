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
        NoAuthentication,
    }

    public class UserManagerService : IDisposable
    {
        private readonly CancellationTokenSource _tokenSource;
        private readonly ConcurrentList<User> _userList;
        private readonly ConcurrentList<ulong> _bannedUserList;
        private readonly ConcurrentList<ulong> _mutedUserList;
        private readonly ConcurrentList<TempbanEntry> _tempbanList;

        public UserManagerService()
        {
            _userList = new ConcurrentList<User>();

            if (File.Exists("banned_users"))
                _bannedUserList = JsonConvert.DeserializeObject<ConcurrentList<ulong>>(File.ReadAllText("banned_users"));
            else
                _bannedUserList = new ConcurrentList<ulong>();

            if (File.Exists("tempbanned_users"))
                _tempbanList = JsonConvert.DeserializeObject<ConcurrentList<TempbanEntry>>(File.ReadAllText("tempbanned_users"));
            else
                _tempbanList = new ConcurrentList<TempbanEntry>();

            if (File.Exists("muted_users"))
                _mutedUserList = JsonConvert.DeserializeObject<ConcurrentList<ulong>>(File.ReadAllText("muted_users"));
            else
                _mutedUserList = new ConcurrentList<ulong>();

            _tokenSource = new CancellationTokenSource();
            _ = CheckTempBans(_tokenSource.Token);
            _ = BackupLists(_tokenSource.Token);
        }

        public MogmogOperationResult AddUser(User user)
        {
            _userList.TryAdd(user);
            return MogmogOperationResult.Success;
        }

        public void RemoveUser(User user)
            => _userList.Remove(user, out _);

        public User GetUser(string name, int worldId)
            => _userList.FirstOrDefault((u) => u.Name == name && u.WorldId == worldId);

        public async Task<User> GetDiscordUser(ulong id)
        {
            foreach (var user in _userList)
            {
                var uid = await user.GetId();
                if (uid == id)
                    return user;
            }
            return null;
        }

        public async Task<MogmogOperationResult> MuteUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await MuteUser(user);
        }

        public async Task<MogmogOperationResult> MuteUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _mutedUserList.TryAdd((await user.GetId()).GetValueOrDefault());
            return MogmogOperationResult.Success;
        }

        public async Task<MogmogOperationResult> UnmuteUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await UnmuteUser(user);
        }

        public async Task<MogmogOperationResult> UnmuteUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _mutedUserList.Remove((await user.GetId()).GetValueOrDefault(), out _);
            return MogmogOperationResult.Success;
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await HasMutedUser(user);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasMutedUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.NoAuthentication);
            return new Tuple<bool, MogmogOperationResult>(_mutedUserList.Contains((await user.GetId()).GetValueOrDefault()), MogmogOperationResult.Success);
        }

        public async Task<MogmogOperationResult> BanUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await BanUser(user);
        }

        public async Task<MogmogOperationResult> BanUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _bannedUserList.TryAdd((await user.GetId()).GetValueOrDefault());
            user.Disconnect();
            return MogmogOperationResult.Success;
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await HasBannedUser(user);
        }

        public async Task<Tuple<bool, MogmogOperationResult>> HasBannedUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return new Tuple<bool, MogmogOperationResult>(false, MogmogOperationResult.NoAuthentication);
            return new Tuple<bool, MogmogOperationResult>(_bannedUserList.Contains((await user.GetId()).GetValueOrDefault()), MogmogOperationResult.Success);
        }

        public async Task<MogmogOperationResult> UnbanUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await UnbanUser(user);
        }

        public async Task<MogmogOperationResult> UnbanUser(User user)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            _bannedUserList.Remove((await user.GetId()).GetValueOrDefault(), out _);
            await UnTempbanUser(user);
            return MogmogOperationResult.Success;
        }

        public async Task<MogmogOperationResult> TempbanUser(string name, int worldId, DateTime end)
        {
            var user = GetUser(name, worldId);
            return await TempbanUser(user, end);
        }

        public async Task<MogmogOperationResult> TempbanUser(User user, DateTime end)
        {
            ThrowIfUserNull(user);
            if (user.AuthState == null)
                return MogmogOperationResult.NoAuthentication;
            await BanUser(user);
            _tempbanList.TryAdd(new TempbanEntry
            {
                UserId = (await user.GetId()).GetValueOrDefault(),
                Name = user.Name,
                WorldId = user.WorldId,
                EndTime = end,
            });
            return MogmogOperationResult.Success;
        }

        public async Task<MogmogOperationResult> UnTempbanUser(string name, int worldId)
        {
            var user = GetUser(name, worldId);
            return await UnTempbanUser(user);
        }

        public async Task<MogmogOperationResult> UnTempbanUser(User user)
        {
            ThrowIfUserNull(user);
            var id = await user.GetId();
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
                await File.WriteAllTextAsync("banned_users", JsonConvert.SerializeObject(_bannedUserList), token);
                await File.WriteAllTextAsync("tempbanned_users", JsonConvert.SerializeObject(_tempbanList), token);
                await File.WriteAllTextAsync("muted_users", JsonConvert.SerializeObject(_mutedUserList), token);
                await Task.Delay(5000, token);
            }
        }

        private void ThrowIfUserNull(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
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
                    foreach (var user in _userList)
                    {
                        user.Dispose();
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
