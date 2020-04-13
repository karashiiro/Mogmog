namespace Mogmog.Logging
{
    public static class Mogger
    {
        public static ILogger Logger;

        public static void Log(string message)
            => Logger.Log(message);

        public static void LogError(string message)
            => Logger.LogError(message);
    }
}
