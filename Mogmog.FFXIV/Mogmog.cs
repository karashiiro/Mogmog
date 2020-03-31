using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
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

        private DalamudPluginInterface dalamud;
        private HttpClient http;
        private MogmogConfiguration config;
        private MogmogInteropConnectionManager connectionManager;

        private string avatar;
        private string lastPlayerName;

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.http = new HttpClient();
            this.config = /*dalamud.GetPluginConfig() as MogmogConfiguration ?? */new MogmogConfiguration();
            this.config.Hostnames.Add("https://localhost:5001"); // Temporary, use Imgui
            this.connectionManager = new MogmogInteropConnectionManager(this.config, this.http)
            {
                MessageReceivedDelegate = MessageReceived,
            };
            
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                dalamud.CommandManager.AddHandler($"/global{i}", OnMessageCommandInfo(i));
                dalamud.CommandManager.AddHandler($"/gl{i}", OnMessageCommandInfo(i, false));
            }
            dalamud.CommandManager.AddHandler("/mgmgconnect", new CommandInfo(AddHost)
            {
                HelpMessage = "Connect to a Mogmog server using its address.",
                ShowInHelp = true,
            });
            dalamud.CommandManager.AddHandler("/mgmgdisconnect", new CommandInfo(RemoveHost)
            {
                HelpMessage = "Disconnect from a Mogmog server using its address.",
                ShowInHelp = true,
            });
        }

        private void MessageSend(string command, string message)
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
            int channelId = int.Parse(command.Substring(command.StartsWith("/global") ? 7 : 3));
            chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{player.Name}{MogmogResources.CrossWorldIcon}{player.HomeWorld.GameData.Name}> {message} *outbound"),
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
            this.connectionManager.MessageSend(chatMessage, channelId);
        }

        private async Task LoadAvatar(PlayerCharacter player)
        {
            var charaName = player.Name;
            var worldName = player.HomeWorld.GameData.Name;
            var uri = new Uri($"https://xivapi.com/character/search?name={charaName}&server={worldName}");
            // On the one hand, it's a waste of resources to have more than one HttpClient, but on the other Dalamud doesn't provide one and it's literally only used in this one function.
            try
            {
                this.dalamud.Log("Making request to {Uri}", uri.OriginalString);
                var res = await this.http.GetStringAsync(uri);
                this.avatar = JObject.Parse(res)["Results"][0]["Avatar"].ToObject<string>();
            }
            catch {} // If XIVAPI is down or broken, whatever
            this.lastPlayerName = player.Name;
            this.dalamud.Log("Player avatar is located at {Uri}.", this.avatar ?? "undefined");
            if (this.avatar == null)
                this.avatar = string.Empty;
        }

        private void MessageReceived(ChatMessage message, int channelId)
        {
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{message.Author}{MogmogResources.CrossWorldIcon}{message.World}> {message.Content} *inbound"),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }

        private void AddHost(string command, string args)
        {
            this.connectionManager.AddHost(args);
            this.dalamud.Framework.Gui.Chat.Print($"Added connection {args}");
        }

        private void RemoveHost(string command, string args)
        {
            this.connectionManager.RemoveHost(args);
            this.dalamud.Framework.Gui.Chat.Print($"Removed connection {args}");
        }

        private CommandInfo OnMessageCommandInfo(int i, bool showInHelp = true)
        {
            return new CommandInfo(MessageSend)
            {
                HelpMessage = $"Sends a message to the Mogmog global chat channel {i}. Shortcut: /gl{i}",
                ShowInHelp = showInHelp,
            };
        }

        public void Dispose()
        {
            this.connectionManager.Dispose();

            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                dalamud.CommandManager.RemoveHandler($"/global{i}");
                dalamud.CommandManager.RemoveHandler($"/gl{i}");
            }

            /*this.dalamud.SavePluginConfig(this.config);*/

            this.dalamud.Dispose();
        }
    }
}
