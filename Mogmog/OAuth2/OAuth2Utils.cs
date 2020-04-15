using System;
using System.Text.RegularExpressions;

namespace Mogmog
{
    public static class OAuth2Utils
    {
        public static string GenerateStateString(int seedLength)
        {
            byte[] rawOutput = new byte[seedLength];
            var rand = new Random();
            rand.NextBytes(rawOutput);
            var state = Convert.ToBase64String(rawOutput);
            state = Regex.Replace(state, @"[^0-9a-zA-Z]+", "");
            return state;
        }
    }
}
