using Dalamud.Game.Chat;
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

        private string avatar;

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.chat = this.dalamud.Framework.Gui.Chat;
            this.config = /*dalamud.GetPluginConfig() as MogmogConfiguration ?? */new MogmogConfiguration();
            this.config.Hostnames.Add("https://localhost:5001"); // Temporary, use Imgui
            this.connectionManager = new MogmogInteropConnectionManager(this.config);
            
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                dalamud.CommandManager.AddHandler($"/global{i}", OnMessageCommandInfo());
                dalamud.CommandManager.AddHandler($"/gl{i}", OnMessageCommandInfo());
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
                Name = "(" + this.dalamud.ClientState.LocalPlayer.Name + " " + MogmogResources.CrossWorldIcon + " " + this.dalamud.ClientState.LocalPlayer.HomeWorld.GameData.Name + ")",
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]" + message),
                Type = XivChatType.Notice,
            });
            this.chat.UpdateQueue(this.dalamud.Framework);
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

        private void LoadAvatar()
        {
            var uri = new Uri($"https://xivapi.com/character/search?name={this.dalamud.ClientState.LocalPlayer.Name}&server={this.dalamud.ClientState.LocalPlayer.HomeWorld.GameData.Name}");
            this.avatar = JObject.Parse(new HttpClient().GetStringAsync(uri).Result)["Results"][0]["Avatar"].ToObject<string>();
        }

        private void MessageReceived(ChatMessage message, int channelId)
        {
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                Name = "(" + message.Author + " " + MogmogResources.CrossWorldIcon + " " + message.World + ")",
                MessageBytes = Encoding.UTF8.GetBytes($"[GL{channelId}]" + message.Content),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }

        private void AddHost(string command, string args)
        {
            this.connectionManager.AddHost(args);
            Echo($"Added connection {args}");
        }

        private void RemoveHost(string command, string args)
        {
            this.connectionManager.RemoveHost(args);
            Echo($"Removed connection {args}");
        }

        private void ShowWindow(string command, string args)
        {
            this.connectionManager.ShowWindow();
            Echo("Window shown.");
        }

        private void HideWindow(string command, string args)
        {
            this.connectionManager.HideWindow();
            Echo("Window hidden.");
        }

        private void Echo(string message)
        {
            this.chat.PrintChat(new XivChatEntry
            {
                MessageBytes = Encoding.UTF8.GetBytes(message),
                Type = XivChatType.Echo,
            });
            this.chat.UpdateQueue(this.dalamud.Framework);
        }

        private CommandInfo OnMessageCommandInfo()
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
