using Dalamud.Plugin;
using Mogmog.FFXIV;
using Mogmog.OAuth2;

namespace Mogmog.Tests.Stubs
{
    public class TestMogmogPlugin : MogmogPlugin
    {
        public void SetCommandHandler(IChatCommandHandler commandHandler)
            => this.CommandHandler = commandHandler;

        public void SetConnectionManager(IConnectionManager connectionManager)
            => this.ConnectionManager = connectionManager;

        public void SetConfig(MogmogConfiguration config)
            => this.Config = config;

        public void SetDalamudPluginInterface(DalamudPluginInterface pi)
            => this.Dalamud = pi;
    }
}
