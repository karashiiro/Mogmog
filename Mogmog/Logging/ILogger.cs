﻿namespace Mogmog.Logging
{
    public interface ILogger
    {
        void Log(string message);

        void LogError(string message);
    }
}
