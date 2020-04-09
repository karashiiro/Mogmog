using Dalamud.Plugin;
using Mogmog.FFXIV;

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
    }
}
