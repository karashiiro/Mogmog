using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Mogmog.Events;
using Mogmog.OAuth2;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV
{
    static class MogmogResources
    {
        public static readonly string CrossWorldIcon = Encoding.UTF8.GetString(new byte[] {
            0x02, 0x12, 0x02, 0x59, 0x03
        });
    }

    public class MogmogPlugin : IDalamudPlugin
    {
        public string Name => "Mogmog";

        private HttpClient http;
        private string avatar;
        private string lastPlayerName;

        protected ICommandHandler CommandHandler { get; set; }
        protected IConnectionManager ConnectionManager { get; set; }
        protected IOAuth2Kit OAuth2 { get; set; }
        protected DalamudPluginInterface Dalamud { get; set; }
        protected MogmogConfiguration Config { get; set; }

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.OAuth2 = new DiscordOAuth2();
            this.OAuth2.LogEvent += Log;
            this.http = new HttpClient();

            this.Dalamud = dalamud;
            //this.config = dalamud.GetPluginConfig() as MogmogConfiguration;
            this.ConnectionManager = new MogmogInteropConnectionManager(this.Config, this.http);
            this.ConnectionManager.MessageReceivedEvent += MessageReceived;
            this.ConnectionManager.LogEvent += Log;
            this.CommandHandler = new CommandHandler(this, this.Config, this.Dalamud);

            //this.oauth2.Authenticate();
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void AddHost(string command, string hostname)
        {
            this.Config.Hostnames.Add(hostname);
            this.ConnectionManager.AddHost(hostname, this.OAuth2.OAuth2Code);
            var idx = this.Config.Hostnames.IndexOf(hostname);
            this.CommandHandler.AddCommandHandler(idx + 1);
            PrintLogMessage($"Added connection {hostname}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void RemoveHost(string command, string hostname)
        {
            if (int.TryParse(hostname, out int i))
                hostname = this.Config.Hostnames[i - 1];
            var idx = this.Config.Hostnames.IndexOf(hostname);
            this.CommandHandler.RemoveCommandHandler(idx + 1);
            this.Config.Hostnames.Remove(hostname);
            this.ConnectionManager.RemoveHost(hostname);
            PrintLogMessage($"Removed connection {hostname}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void ReloadHost(string command, string hostname)
        {
            if (int.TryParse(hostname, out int i))
                hostname = this.Config.Hostnames[i - 1];
            this.ConnectionManager.ReloadHost(hostname);
            PrintLogMessage($"Reloaded connection {hostname}");
        }

        public void MessageSend(string command, string message)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            _ = MessageSendAsync(command, message);
        }

        private async Task MessageSendAsync(string command, string message)
        {
            var player = this.Dalamud.ClientState.LocalPlayer;

            // Handles switching characters
            if (this.lastPlayerName == null || this.lastPlayerName != player.Name)
                await LoadAvatar(player);

            // 7 if /global, 3 if /gl
            int channelId = int.Parse(command.Substring(command.StartsWith("/global", StringComparison.InvariantCultureIgnoreCase) ? 7 : 3), CultureInfo.InvariantCulture);

            PrintChatMessage(channelId, player, message);

            var chatMessage = new ChatMessage
            {
                Id = 0,
                Content = message,
                Author = player.Name,
                AuthorId = this.Dalamud.ClientState.LocalContentId,
                Avatar = this.avatar,
                World = string.Empty,
                WorldId = player.HomeWorld.Id,
            };
            this.ConnectionManager.MessageSend(chatMessage, channelId - 1);
        }

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.AuthorId == this.Dalamud.ClientState.LocalContentId)
                return;
            PrintChatMessage(e);
        }

        private void PrintChatMessage(int channelId, PlayerCharacter player, string message)
            => PrintChatMessage(channelId, player.Name, player.HomeWorld.GameData.Name, message);

        private void PrintChatMessage(MessageReceivedEventArgs e)
            => PrintChatMessage(e.ChannelId, e.Message.Author, e.Message.World, e.Message.Content);

        private void PrintChatMessage(int channelId, string playerName, string worldName, string message)
        {
            var chat = this.Dalamud.Framework.Gui.Chat;
            chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{playerName}{MogmogResources.CrossWorldIcon}{worldName}> {message}"),
                Type = XivChatType.Notice,
            });
            chat.UpdateQueue(this.Dalamud.Framework);
        }

        private void PrintLogMessage(string message)
        {
            this.Dalamud?.Framework.Gui.Chat.Print(message);
        }

        private void Log(object sender, LogEventArgs e)
        {
            if (e.IsError)
                this.Dalamud.LogError(e.LogMessage);
            else
                this.Dalamud.Log(e.LogMessage);
        }
        
        private async Task LoadAvatar(PlayerCharacter player)
        {
            var charaName = player.Name;
            var worldName = player.HomeWorld.GameData.Name;
            var uri = new Uri($"https://xivapi.com/character/search?name={charaName}&server={worldName}");
            try
            {
                this.Dalamud.Log("Making request to {Uri}", uri.OriginalString);
                var res = await this.http.GetStringAsync(uri);
                this.avatar = JObject.Parse(res)["Results"][0]["Avatar"].ToObject<string>();
            }
            catch (HttpRequestException e)
            {
                // If XIVAPI is down or broken, whatever
                this.Dalamud.LogError("XIVAPI returned an error: " + e.Message);
                this.Dalamud.LogError(e.StackTrace);
            }
            this.lastPlayerName = player.Name;
            this.Dalamud.Log("Player avatar is located at {Uri}.", this.avatar ?? "undefined");
            if (this.avatar == null)
                this.avatar = string.Empty;
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.ConnectionManager.MessageReceivedEvent -= MessageReceived;
                    this.ConnectionManager.LogEvent -= Log;

                    this.CommandHandler.Dispose();
                    this.ConnectionManager.Dispose();

                    this.http.Dispose();

                    /*this.dalamud.SavePluginConfig(this.config);*/

                    this.Dalamud.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
