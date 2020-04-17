using System;
using System.Threading.Tasks;

namespace Mogmog
{
    public class User : IDisposable
    {
        public event EventHandler ForcedDisconnect;

        public int WorldId { get; }

        public string Name { get; }

        public DiscordOAuth2 AuthState { get; set; }

        public UserObjectResponse Info { get; private set; }

        public User(string name, int worldId)
        {
            Name = name;
            WorldId = worldId;
        }

        public async Task<UserObjectResponse> GetUserInfo()
        {
            if (Info != null)
                return Info;
            Info = await AuthState.GetUserInfo();
            return Info;
        }

        public async Task<ulong?> GetId()
        {
            if (AuthState == null)
                return null;
            if (Info != null)
                return Info.Id;
            Info = await AuthState.GetUserInfo();
            return Info.Id;
        }

        public void Disconnect()
        {
            var handler = ForcedDisconnect;
            handler?.Invoke(this, new EventArgs());
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    AuthState.Dispose();
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
