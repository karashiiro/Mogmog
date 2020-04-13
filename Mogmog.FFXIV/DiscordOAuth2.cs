﻿using Mogmog.Events;
using Mogmog.Exceptions;
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

namespace Mogmog.FFXIV
{
    public class DiscordOAuth2 : IOAuth2Kit
    {
        private const string oAuth2BaseUrl = "https://discordapp.com/api/oauth2/authorize?client_id=698205573388435518&redirect_uri=https%3A%2F%2Flocalhost%3A{0}%2Flogin%2Fdiscord&response_type=code&scope=identify&state={1}";
        private static readonly int[] reservedPorts = new int[] { 5002, 13648, 30124 };

        private bool handlerCompleted;

        public event EventHandler<LogEventArgs> LogEvent;

        public bool IsAuthenticated { get; private set; }
        public string OAuth2Code { get; private set; }

        public void Authenticate()
        {
            Log("Attempting to authenticate with Discord.");
            this.handlerCompleted = false;
            var stateString = OAuth2Utils.GenerateStateString(20);
            HttpServer authServer = null;
            for (int i = 0; i < reservedPorts.Length && authServer == null; i++)
            {
                try
                {
                    authServer = new HttpServer(reservedPorts[i], (line) => Log(line));
                }
                catch (PortUnavailableException e)
                {
                    LogError(e.Message);
                    if (i == reservedPorts.Length - 1)
                        throw;
                    else
                        continue;
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
                throw new CSRFInvalidationException("CSRF attack suspected! Please report this error on Discord; it may indicate a vulnerability in the application.");
            OAuth2Code = queryParams["code"];
            this.handlerCompleted = true;
            return HttpServerPipelineResult.Handled;
        }

        private string GetOAuth2Url(int port, string stateString)
            => string.Format(CultureInfo.InvariantCulture, oAuth2BaseUrl, port, stateString);

        // https://stackoverflow.com/a/43232486
        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
                else
                    throw;
            }
        }

        private void Log(string message, bool isError = false)
        {
            var handler = LogEvent;
            var logMessage = new LogEventArgs
            {
                LogMessage = message,
                IsError = isError,
            };
            handler?.Invoke(this, logMessage);
        }

        private void LogError(string message)
            => Log(message, true);
    }
}
