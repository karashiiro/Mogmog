using Mogmog.Logging;
using System.Globalization;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class ProgramLogger : ILogger
    {
        public void Log(string message)
            => _ = LogAsync(message);

        public void LogError(string message)
            => _ = LogErrorAsync(message);

        private static async Task LogAsync(string message)
        {
            var interopLog = new GenericInterop
            {
                Command = message,
                Arg = false.ToString(CultureInfo.InvariantCulture),
            };
            await Program.SendToParent(interopLog);
        }

        private static async Task LogErrorAsync(string message)
        {
            var interopLog = new GenericInterop
            {
                Command = message,
                Arg = true.ToString(CultureInfo.InvariantCulture),
            };
            await Program.SendToParent(interopLog);
        }
    }
}
