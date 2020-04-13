using System;

namespace Mogmog
{
    public static class OAuth2Utils
    {
        public static string GenerateStateString(int seedLength)
        {
            byte[] rawOutput = new byte[seedLength];
            var rand = new Random();
            rand.NextBytes(rawOutput);
            return Convert.ToBase64String(rawOutput);
        }
    }
}
