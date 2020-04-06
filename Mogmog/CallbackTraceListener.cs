using System;
using System.Diagnostics;

namespace Mogmog
{
    public class LogEventArgs : EventArgs
    {
        public string LogMessage { get; set; }
        public bool IsError { get; set; }
    }

    public class CallbackTraceListener : TraceListener
    {
        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public event LogEventHandler LogEvent;

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
