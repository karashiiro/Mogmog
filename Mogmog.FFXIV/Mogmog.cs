using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Mogmog.FFXIV
{
    static class MogmogRegexes
    {
        public static readonly Regex DigitsOnly = new Regex(@"\d+", RegexOptions.Compiled);
    }

    public class Mogmog : IDalamudPlugin
    {
        public string Name => "Mogmog";

        private DalamudPluginInterface dalamud;
        private MogmogConfiguration config;
        private MogmogConnectionManager connectionManager;

        private string CharacterSearch { get => $"https://xivapi.com/character/search?name={this.dalamud.ClientState.LocalPlayer.Name}&server={this.dalamud.ClientState.LocalPlayer.HomeWorld.Name}"; }
        private string avatar;

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.config = /*dalamud.GetPluginConfig() as MogmogConfiguration ?? */new MogmogConfiguration();
            this.config.Hostnames.Add("https://localhost:5001"); // Temporary, use Imgui
            this.connectionManager = new MogmogConnectionManager(this.config, this.dalamud.CommandManager, this)
            {
                MessageRecievedDelegate = MessageReceived,
            };

            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                dalamud.CommandManager.AddHandler($"/global{i}", OnMessageCommandInfo());
                dalamud.CommandManager.AddHandler($"/gl{i}", OnMessageCommandInfo());
            }

            this.avatar = JObject.Parse(new HttpClient().GetStringAsync(new Uri(CharacterSearch)).Result)["Results"][0]["Avatar"].ToObject<string>();
        }

        private void MessageSend(string command, string message)
        {
            int channelId = int.Parse(MogmogRegexes.DigitsOnly.Match(command).Value);
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                Name = this.dalamud.ClientState.LocalPlayer.Name + " (" + this.dalamud.ClientState.LocalPlayer.HomeWorld.Name + ")", // todo: use CW icon
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]" + message),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
            var chatMessage = new ChatMessage
            {
                Id = 0,
                Content = message,
                Author = this.dalamud.ClientState.LocalPlayer.Name,
                AuthorId = this.dalamud.ClientState.LocalContentId,
                Avatar = this.avatar,
                World = null,
                WorldId = this.dalamud.ClientState.LocalPlayer.HomeWorld.Id,
            };
            this.connectionManager.MessageSend(chatMessage, channelId);
        }

        private void MessageReceived(ChatMessage message, int channelId)
        {
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                Name = message.Author + " (" + message.World + ")", // todo: use CW icon
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]" + message.Content),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }

        public CommandInfo OnMessageCommandInfo()
        {
            return new CommandInfo(MessageSend)
            {
                HelpMessage = "Sends a message to the Mogmog global chat.",
                ShowInHelp = true,
            };
        }

        public void Dispose()
        {
            this.connectionManager.Dispose();

            for (int i = 0; i < this.config.Hostnames.Count; i++)
            {
                dalamud.CommandManager.RemoveHandler($"/global{i}");
                dalamud.CommandManager.RemoveHandler($"/gl{i}");
            }

            /*this.dalamud.SavePluginConfig(this.config);*/

            this.dalamud.Dispose();
        }
    }
}
