using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Internal.Gui;
using Dalamud.Plugin;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Mogmog.FFXIV
{
    static class MogmogResources
    {
        public static readonly Regex DigitsOnly = new Regex(@"\d+", RegexOptions.Compiled);

        public static readonly string CrossWorldIcon = Encoding.UTF8.GetString(new byte[] {
            0x02, 0x12, 0x02, 0x59, 0x03
        });
    }

    public class Mogmog : IDalamudPlugin
    {
        public string Name => "Mogmog";

        private ChatGui chat;
        private DalamudPluginInterface dalamud;
        private MogmogConfiguration config;
        private MogmogInteropConnectionManager connectionManager;
        private PlayerCharacter player;

        private string avatar;

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.chat = this.dalamud.Framework.Gui.Chat;
            this.player = this.dalamud.ClientState.LocalPlayer;
            this.config = /*dalamud.GetPluginConfig() as MogmogConfiguration ?? */new MogmogConfiguration();
            this.config.Hostnames.Add("https://localhost:5001"); // Temporary, use Imgui
            this.connectionManager = new MogmogInteropConnectionManager(this.config);
            
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
            dalamud.CommandManager.AddHandler("/mgmgshow", new CommandInfo(ShowWindow)
            {
                HelpMessage = "Show the Mogmog console window.",
                ShowInHelp = true,
            });
            dalamud.CommandManager.AddHandler("/mgmghide", new CommandInfo(HideWindow)
            {
                HelpMessage = "Hide the Mogmog console window.",
                ShowInHelp = true,
            });

            this.connectionManager.MessageReceivedDelegate = MessageReceived;
        }

        private void MessageSend(string command, string message)
        {
            if (this.avatar == null)
                LoadAvatar();

            int channelId = int.Parse(MogmogResources.DigitsOnly.Match(command).Value);
            this.chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{this.player.Name}> {message}"),
                Type = XivChatType.Notice,
            });
            this.chat.UpdateQueue(this.dalamud.Framework);
            var chatMessage = new ChatMessage
            {
                Id = 0,
                Content = message,
                Author = this.player.Name,
                AuthorId = this.dalamud.ClientState.LocalContentId,
                Avatar = this.avatar,
                World = string.Empty,
                WorldId = this.player.HomeWorld.Id,
            };
            this.connectionManager.MessageSend(chatMessage, channelId);
        }

        private void LoadAvatar()
        {
            var uri = new Uri($"https://xivapi.com/character/search?name={this.player.Name}&server={this.player.HomeWorld.GameData.Name}");
            this.avatar = JObject.Parse(new HttpClient().GetStringAsync(uri).Result)["Results"][0]["Avatar"].ToObject<string>();
        }

        private void MessageReceived(ChatMessage message, int channelId)
        {
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]<{message.Author}> {message.Content}"),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }

        private void AddHost(string command, string args)
        {
            this.connectionManager.AddHost(args);
            this.chat.Print($"Added connection {args}");
        }

        private void RemoveHost(string command, string args)
        {
            this.connectionManager.RemoveHost(args);
            this.chat.Print($"Removed connection {args}");
        }

        private void ShowWindow(string command, string args)
        {
            this.connectionManager.ShowWindow();
            this.chat.Print("Window shown.");
        }

        private void HideWindow(string command, string args)
        {
            this.connectionManager.HideWindow();
            this.chat.Print("Window hidden.");
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
