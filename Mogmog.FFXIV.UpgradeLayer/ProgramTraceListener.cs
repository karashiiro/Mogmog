using Mogmog.Logging;
using System.Diagnostics;

namespace Mogmog
{
    public class ProgramTraceListener : TraceListener
    {
        public override void Write(string message)
            => Mogger.LogError(message);

        public override void WriteLine(string message)
            => Mogger.LogError(message);
    }
}
