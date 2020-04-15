﻿using Newtonsoft.Json;
using Serilog;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Server
{
    public class DiscordOAuth2 : IDisposable
    {
        private const string redirectUri = "http://localhost:5002/login/discord";

        private readonly CancellationTokenSource tokenSource;

        public AccessCodeResponse AccessInformation { get; set; }

        public DiscordOAuth2()
        {
            this.tokenSource = new CancellationTokenSource();
            _ = RefreshAccessInformation(this.tokenSource.Token);
        }

        private async Task RefreshAccessInformation(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                if (AccessInformation == null)
                {
                    await Task.Delay(1000);
                    continue;
                }
                await Task.Delay((AccessInformation.ExpiresIn - 100) * 1000, token);
                AccessInformation = await Refresh(AccessInformation.RefreshToken);
            }
        }

        public static async Task<AccessCodeResponse> Authorize(string accessCode)
        {
            var postData = new FormData();
            postData.Add("client_id", Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_ID"));
            postData.Add("client_secret", Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_SECRET"));
            postData.Add("grant_type", "authorization_code");
            postData.Add("redirect_uri", redirectUri);
            postData.Add("scope", "identify");
            postData.Add("code", accessCode);

            using var http = new HttpClient();
            using var content = new StringContent(postData.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
            var res = await http.PostAsync(new Uri("https://discordapp.com/api/oauth2/token"), content);
            if (!res.IsSuccessStatusCode)
            {
                Log.Error("Discord user authentication for access code {AccessCode} failed, reason: {ErrorMessage}", accessCode, res.ReasonPhrase);
                return null;
            }

            return JsonConvert.DeserializeObject<AccessCodeResponse>(await res.Content.ReadAsStringAsync());
        }

        public static async Task<AccessCodeResponse> Refresh(string refreshToken)
        {
            var postData = new FormData();
            postData.Add("client_id", Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_ID"));
            postData.Add("client_secret", Environment.GetEnvironmentVariable("MOGMOG_SERVER_CLIENT_SECRET"));
            postData.Add("grant_type", "refresh_token");
            postData.Add("refresh_token", refreshToken);
            postData.Add("redirect_uri", redirectUri);
            postData.Add("scope", "identify");

            using var http = new HttpClient();
            using var content = new StringContent(postData.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
            var res = await http.PostAsync(new Uri("https://discordapp.com/api/oauth2/token"), content);
            if (!res.IsSuccessStatusCode)
                return null;

            return JsonConvert.DeserializeObject<AccessCodeResponse>(await res.Content.ReadAsStringAsync());
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.tokenSource.Cancel();
                    this.tokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class AccessCodeResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; }

        [JsonProperty("token_type")]
        public string TokenType { get; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; }

        [JsonProperty("scope")]
        public string Scope { get; }

        [JsonIgnore]
        public bool Bypass { get; set; }
    }
}
