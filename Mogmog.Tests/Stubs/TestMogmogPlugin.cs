using Dalamud.Plugin;
using Mogmog.FFXIV;
using Mogmog.OAuth2;

namespace Mogmog.Tests.Stubs
{
    public class TestMogmogPlugin : MogmogPlugin
    {
        public void SetCommandHandler(ICommandHandler commandHandler)
            => this.CommandHandler = commandHandler;

        public void SetConnectionManager(IConnectionManager connectionManager)
            => this.ConnectionManager = connectionManager;

        public void SetConfig(MogmogConfiguration config)
            => this.Config = config;

        public void SetDalamudPluginInterface(DalamudPluginInterface pi)
            => this.Dalamud = pi;

        public void SetOAuth2(IOAuth2Kit oauth2)
            => this.OAuth2 = oauth2;
    }
}
