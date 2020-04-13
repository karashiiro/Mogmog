using Mogmog.Events;
using System;

namespace Mogmog.OAuth2
{
    public interface IOAuth2Kit
    {
        string OAuth2Code { get; }

        event EventHandler<LogEventArgs> LogEvent;

        void Authenticate();
    }
}
