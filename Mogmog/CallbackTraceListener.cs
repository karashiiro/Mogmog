using Mogmog.Events;
using System;
using System.Diagnostics;

namespace Mogmog
{
    public class CallbackTraceListener : TraceListener
    {
        public event EventHandler<LogEventArgs> LogEvent;

        public override void Write(string message)
        {
            LogEvent(this, new LogEventArgs { LogMessage = message, IsError = true });
        }

        public override void WriteLine(string message)
        {
            LogEvent(this, new LogEventArgs { LogMessage = message, IsError = true });
        }
    }
}
