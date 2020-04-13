using Dalamud.Plugin;
using Mogmog.Logging;

namespace Mogmog.FFXIV
{
    public class DalamudLogger : ILogger
    {
        private readonly DalamudPluginInterface dalamud;

        public DalamudLogger(DalamudPluginInterface dalamud)
            => this.dalamud = dalamud;

        public void Log(string message)
            => this.dalamud.Log(message);

        public void LogError(string message)
            => this.dalamud.LogError(message);
    }
}
