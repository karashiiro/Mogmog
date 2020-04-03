using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
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

    public class Mogmog : IDalamudPlugin
    {
        public string Name => "Mogmog";

        private CommandHandler commandHandler;
        private DalamudPluginInterface dalamud;
        private HttpClient http;
        private MogmogConfiguration config;
        private MogmogInteropConnectionManager connectionManager;

        private string avatar;
        private string lastPlayerName;

        public Mogmog()
        {
            this.config = new MogmogConfiguration();
        }

        public Mogmog(MogmogConfiguration config)
        {
            this.config = config;
        }

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.http = new HttpClient();
            //this.config = dalamud.GetPluginConfig() as MogmogConfiguration;
            this.connectionManager = new MogmogInteropConnectionManager(this.config, this.http);
            this.connectionManager.MessageReceivedEvent += MessageReceived;
            this.commandHandler = new CommandHandler(this, this.config, this.dalamud);
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void AddHost(string command, string args)
        {
            this.connectionManager.AddHost(args);
            this.commandHandler.AddChatHandler(this.config.Hostnames.IndexOf(args) + 1);
            this.dalamud.Framework.Gui.Chat.Print($"Added connection {args}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void RemoveHost(string command, string args)
        {
            this.commandHandler.RemoveChatHandler(this.config.Hostnames.IndexOf(args) + 1);
            this.connectionManager.RemoveHost(args);
            this.dalamud.Framework.Gui.Chat.Print($"Removed connection {args}");
        }

        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "This will never be null.")]
        public void MessageSend(string command, string message)
        {
            _ = MessageSendAsync(command, message);
        }

        private async Task MessageSendAsync(string command, string message)
        {
            var chat = this.dalamud.Framework.Gui.Chat;
            var player = this.dalamud.ClientState.LocalPlayer;

            // Handles switching characters
            if (this.lastPlayerName == null || this.lastPlayerName != player.Name)
                await LoadAvatar(player);

            // 7 if /global, 3 if /gl
            int channelId = int.Parse(command.Substring(command.StartsWith("/global", StringComparison.InvariantCultureIgnoreCase) ? 7 : 3), CultureInfo.InvariantCulture);

            chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{player.Name}{MogmogResources.CrossWorldIcon}{player.HomeWorld.GameData.Name}> {message}"),
                Type = XivChatType.Notice,
            });
            chat.UpdateQueue(this.dalamud.Framework);
            var chatMessage = new ChatMessage
            {
                Id = 0,
                Content = message,
                Author = player.Name,
                AuthorId = this.dalamud.ClientState.LocalContentId,
                Avatar = this.avatar,
                World = string.Empty,
                WorldId = player.HomeWorld.Id,
            };
            this.connectionManager.MessageSend(chatMessage, channelId - 1);
        }

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.AuthorId == this.dalamud.ClientState.LocalContentId)
                return;
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{e.ChannelId}]<{e.Message.Author}{MogmogResources.CrossWorldIcon}{e.Message.World}> {e.Message.Content}"),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }
        
        private async Task LoadAvatar(PlayerCharacter player)
        {
            var charaName = player.Name;
            var worldName = player.HomeWorld.GameData.Name;
            var uri = new Uri($"https://xivapi.com/character/search?name={charaName}&server={worldName}");
            try
            {
                this.dalamud.Log("Making request to {Uri}", uri.OriginalString);
                var res = await this.http.GetStringAsync(uri);
                this.avatar = JObject.Parse(res)["Results"][0]["Avatar"].ToObject<string>();
            }
            catch (HttpRequestException e)
            {
                // If XIVAPI is down or broken, whatever
                this.dalamud.LogError("XIVAPI returned an error: " + e.Message);
                this.dalamud.LogError(e.StackTrace);
            }
            this.lastPlayerName = player.Name;
            this.dalamud.Log("Player avatar is located at {Uri}.", this.avatar ?? "undefined");
            if (this.avatar == null)
                this.avatar = string.Empty;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.connectionManager.MessageReceivedEvent -= MessageReceived;

                    this.commandHandler.Dispose();
                    this.connectionManager.Dispose();

                    this.http.Dispose();

                    /*this.dalamud.SavePluginConfig(this.config);*/

                    this.dalamud.Dispose();
                }

                disposedValue = true;
            }
        }

        [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Class does not need to free unmanaged resources.")]
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
