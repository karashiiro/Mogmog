using Mogmog.Events;
using Mogmog.Exceptions;
using Mogmog.Logging;
using Mogmog.OAuth2;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.SimpleTcpServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    public class DiscordOAuth2 : IOAuth2Kit
    {
        private const string oAuth2BaseUrl = "https://discordapp.com/api/oauth2/authorize?client_id={0}&redirect_uri=https%3A%2F%2Flocalhost%3A{1}%2Flogin%2Fdiscord&response_type=code&scope=identify&state={2}";
        private static readonly int[] reservedPorts = new [] { 5002, 13648, 30124 };

        private bool handlerCompleted;

        public bool IsAuthenticated { get; private set; }
        public string OAuth2Code { get; private set; }

        public void Authenticate()
        {
            Mogger.Log(LogMessages.DiscordAuthInProgress);
            this.handlerCompleted = false;
            var stateString = OAuth2Utils.GenerateStateString(20);
            HttpServer authServer = null;
            for (int i = 0; i < reservedPorts.Length && authServer == null; i++)
            {
                try
                {
                    authServer = new HttpServer(reservedPorts[i], Mogger.Log);
                }
                catch (PortUnavailableException e)
                {
                    Mogger.LogError(e.Message);
                    if (i == reservedPorts.Length - 1)
                        throw;
                }
            }
            authServer.AddHandler((processor, stream) => OAuth2RedirectHandler(processor, stateString));
            OpenUrl(GetOAuth2Url(authServer.Port, stateString));
            while (!this.handlerCompleted)
                Task.Delay(50).Wait();
            authServer.Dispose();
        }

        /// <summary>
        /// Redirect handler for Discord OAuth2.
        /// </summary>
        /// <returns cref="HttpServerPipelineResult">The success status of the handler action.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no access code is returned by the server.</exception>
        private HttpServerPipelineResult OAuth2RedirectHandler(HttpProcessor processor, string state)
        {
            var uri = new Uri(processor.FullUrl);
            var queryParams = uri.ParseQueryString().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (queryParams["state"] != state)
                throw new CSRFInvalidationException(ExceptionMessages.CSRFInvalidation);
            OAuth2Code = queryParams["code"];
            this.handlerCompleted = true;
            return HttpServerPipelineResult.Handled;
        }

        private static string GetOAuth2Url(int port, string stateString)
            => string.Format(CultureInfo.InvariantCulture, oAuth2BaseUrl, "dummy", port, stateString);

        // https://stackoverflow.com/a/43232486
        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&", StringComparison.InvariantCulture)}") { CreateNoWindow = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
                else
                    throw;
            }
        }
    }
}
