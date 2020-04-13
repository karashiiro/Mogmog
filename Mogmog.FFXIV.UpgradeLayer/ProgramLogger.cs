using Mogmog.Logging;
using System.Globalization;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class ProgramLogger : ILogger
    {
        public async void Log(string message)
        {
            var interopLog = new GenericInterop
            {
                Command = message,
                Arg = false.ToString(CultureInfo.InvariantCulture),
            };
            await Program.SendToParent(interopLog);
        }

        public async void LogError(string message)
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
