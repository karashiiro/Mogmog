using Mogmog.Logging;
using System;

namespace Mogmog.Tests.Stubs
{
    public class TestLogger : ILogger
    {
        public int LogCalledTimes { get; set; }

        public TestLogger()
        {
            LogCalledTimes = 0;
        }

        public void Log(string message)
        {
            LogCalledTimes++;
            Console.WriteLine(message);
        }

        public void LogError(string message)
        {
            LogCalledTimes++;
            Console.WriteLine(message);
        }
    }
}
