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
        private const string oAuth2BaseUrl = "https://discordapp.com/api/oauth2/authorize?client_id={0}&redirect_uri=http%3A%2F%2Flocalhost%3A{1}%2Flogin%2Fdiscord&response_type=code&scope=identify&state={2}";
        private static readonly int[] reservedPorts = new [] { 5002, 13648, 30124 };

        private bool handlerCompleted;

        public string OAuth2Code { get; private set; }

        public void Authenticate(string serverAccountId)
        {
            Mogger.Log(LogMessages.DiscordAuthInProgress);
            this.handlerCompleted = false;
            var stateString = OAuth2Utils.GenerateStateString(20);
            HttpServer authServer = null;
            for (var i = 0; i < reservedPorts.Length && authServer == null; i++)
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
            // ReSharper disable once PossibleNullReferenceException
            authServer.AddHtmlDocumentHandler((processor, stream) => OAuth2RedirectHandler(processor, authServer.Port, stateString));
            OpenUrl(GetOAuth2Url(serverAccountId, authServer.Port, stateString));
            while (!this.handlerCompleted)
                Task.Delay(50).Wait();
            authServer.Dispose();
        }

        /// <summary>
        /// Redirect handler for Discord OAuth2.
        /// </summary>
        /// <returns cref="HttpServerPipelineResult">The success status of the handler action.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no access code is returned by the server.</exception>
        private string OAuth2RedirectHandler(HttpProcessor processor, int port, string state)
        {
            var uri = new Uri("http://localhost:" + port + processor.FullUrl);
            var queryParams = uri.ParseQueryString().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Mogger.Log($"Sent state {state}, receiving state {queryParams["state"]}");
            if (queryParams["state"] != state)
                throw new CSRFInvalidationException(ExceptionMessages.CSRFInvalidation);
            OAuth2Code = queryParams["code"];
            this.handlerCompleted = true;
            return "Success"; // TODO: Make a full success page
        }

        private static Uri GetOAuth2Url(string serverAccountId, int port, string stateString)
            => new Uri(string.Format(CultureInfo.InvariantCulture, oAuth2BaseUrl, serverAccountId, port, stateString));

        // https://stackoverflow.com/a/43232486
        private static void OpenUrl(Uri uri)
        {
            try
            {
                Process.Start(uri.AbsoluteUri);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {uri.AbsoluteUri.Replace("&", "^&", StringComparison.InvariantCulture)}") { CreateNoWindow = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", uri.AbsoluteUri);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", uri.AbsoluteUri);
                else
                    throw;
            }
        }
    }
}
