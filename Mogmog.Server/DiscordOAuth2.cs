using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.Server
{
    public class DiscordOAuth2
    {
        private const string redirectUri = "https://localhost:5001/login/discord";

        public AccessCodeResponse AccessToken { get; set; }

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
                return null;

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
    }
}
