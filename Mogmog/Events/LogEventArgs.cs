using System;

namespace Mogmog.Events
{
    public class LogEventArgs : EventArgs
    {
        public string LogMessage { get; set; }
        public bool IsError { get; set; }
    }
}
