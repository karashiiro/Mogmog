﻿using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Mogmog.Events;
using Mogmog.Logging;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mogmog.FFXIV
{
    internal static class MogmogResources
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

        protected IChatCommandHandler CommandHandler { get; set; }
        protected IConnectionManager ConnectionManager { get; set; }
        protected DalamudPluginInterface Dalamud { get; set; }
        protected MogmogConfiguration Config { get; set; }

        public void Initialize(DalamudPluginInterface dalamud)
        {
            Mogger.Logger = new DalamudLogger(dalamud);

            this.http = new HttpClient();
            this.Dalamud = dalamud;
            this.ConnectionManager = new MogmogInteropConnectionManager(this.Config, this.http);
            this.ConnectionManager.MessageReceivedEvent += MessageReceived;
            this.CommandHandler = new CommandHandler(this, this.Config, this.Dalamud);
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void AddHost(string command, string args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            var splitArgs = args.Split(' ');
            var hostname = splitArgs[0];
            var saveAccessCode = false;
            if (splitArgs.Length >= 2)
                saveAccessCode = bool.Parse(splitArgs[1]);
            var host = new Host { Hostname = hostname, SaveAccessCode = saveAccessCode };
            this.Config.Hosts.Add(host);
            this.ConnectionManager.AddHost(hostname, saveAccessCode);
            var idx = this.Config.Hosts.IndexOf(host);
            this.CommandHandler.AddChatHandler(idx + 1);
            PrintLogMessage($"Added connection {hostname}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void RemoveHost(string command, string hostname)
        {
            Host host;
            if (int.TryParse(hostname, out var i))
                host = this.Config.Hosts[i - 1];
            else
                return; // Should eventually print error message
            hostname = host.Hostname;
            var idx = this.Config.Hosts.IndexOf(host);
            this.CommandHandler.RemoveChatHandler(idx + 1);
            this.Config.Hosts.Remove(host);
            this.ConnectionManager.RemoveHost(hostname);
            PrintLogMessage($"Removed connection {hostname}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void ReloadHost(string command, string hostname)
        {
            if (int.TryParse(hostname, out var i))
                hostname = this.Config.Hosts[i - 1].Hostname;
            else
                return;
            this.ConnectionManager.ReloadHost(hostname);
            PrintLogMessage($"Reloaded connection {hostname}");
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void BlockUser(string command, string args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            var splitArgs = args.Split(' ');
            switch (splitArgs.Length)
            {
                case 3: // <Character Name> <World Name>
                    var name = Capitalize(splitArgs[0]) + " " + Capitalize(splitArgs[1]);
                    var world = this.Dalamud.Data.GetExcelSheet<World>()
                        .GetRows()
                        .FirstOrDefault(w => w.Name == Capitalize(splitArgs[2]));
                    if (world != null)
                    {
                        var worldId = world.RowId;
                        Config.BlockedUsers.Add(new UserFragment
                        {
                            Name = name,
                            WorldId = worldId,
                        });
                        PrintLogMessage($"Blocked user {name}{MogmogResources.CrossWorldIcon}{world.Name}");
                    }
                    break;
                case 1: // <Discord ID>
                    if (ulong.TryParse(splitArgs[0], out var userId))
                    {
                        Config.BlockedUsers.Add(new UserFragment
                        {
                            Id = userId.ToString(CultureInfo.CurrentCulture),
                        });
                        PrintLogMessage($"Blocked user {userId}");
                    }
                    break;
                default:
                    PrintLogMessage(LogMessages.FailedToParseCommand + "\n" +
                                    $"{command} <Character Name> <World Name>\n" +
                                    $"{command} <Discord ID>\n");
                    break;
            }
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The parameter is required for the HandlerDelegate type.")]
        public void UnblockUser(string command, string args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            var splitArgs = args.Split(' ');
            if (splitArgs.Length == 3)
            {
                var name = Capitalize(splitArgs[0]) + " " + Capitalize(splitArgs[1]);
                var world = this.Dalamud.Data.GetExcelSheet<World>()
                    .GetRows()
                    .FirstOrDefault(w => w.Name == Capitalize(splitArgs[2]));
                if (world != null)
                {
                    var worldId = world.RowId;
                    Config.BlockedUsers.Remove(new UserFragment
                    {
                        Name = name,
                        WorldId = worldId,
                    });
                    PrintLogMessage($"Unblocked user {name}{MogmogResources.CrossWorldIcon}{world.Name}");
                    return;
                }
            }
            if (splitArgs.Length == 1)
            {
                if (ulong.TryParse(splitArgs[0], out var userId))
                {
                    Config.BlockedUsers.Remove(new UserFragment
                    {
                        Id = userId.ToString(CultureInfo.CurrentCulture),
                    });
                    PrintLogMessage($"Unblocked user {userId}");
                    return;
                }
            }
            PrintLogMessage(LogMessages.FailedToParseCommand + "\n" +
                            $"{command} <Character Name> <World Name>\n" +
                            $"{command} <Discord ID>\n");
        }

        #region Moderation Commands
        public void BanUser(string command, string args)
            => ProcessThenRun(command, args, this.ConnectionManager.BanUser);

        public void UnbanUser(string command, string args)
            => ProcessThenRun(command, args, this.ConnectionManager.UnbanUser);

        public void TempbanUser(string command, string args)
        {
            var player = this.Dalamud.ClientState.LocalPlayer;
            var parsedArgs = ProcessTargetUserArgs(command, args, "<Unban Date and/or Time>");
            if (parsedArgs == null)
                return;
            var (name, worldId, channelId) = parsedArgs;
            var end = DateTimeUtils.GetDateTime(args);
            this.ConnectionManager.TempbanUser(name, worldId, end, player.Name, player.HomeWorld.Id, channelId);
        }

        public void KickUser(string command, string args)
            => ProcessThenRun(command, args, this.ConnectionManager.KickUser);

        public void MuteUser(string command, string args)
            => ProcessThenRun(command, args, this.ConnectionManager.MuteUser);

        public void UnmuteUser(string command, string args)
            => ProcessThenRun(command, args, this.ConnectionManager.UnmuteUser);

        private void ProcessThenRun(string command, string args, Action<string, int, string, int, int> fnToRun)
        {
            var player = this.Dalamud.ClientState.LocalPlayer;
            var parsedArgs = ProcessTargetUserArgs(command, args);
            if (parsedArgs == null)
                return;
            var (name, worldId, channelId) = parsedArgs;
            fnToRun(name, worldId, player.Name, player.HomeWorld.Id, channelId);
        }

        private Tuple<string, int, int> ProcessTargetUserArgs(string command, string rawArgs)
            => ProcessTargetUserArgs(command, rawArgs, string.Empty);

        private Tuple<string, int, int> ProcessTargetUserArgs(string command, string rawArgs, string additionalArgHints)
        {
            if (rawArgs == null)
                throw new ArgumentNullException(nameof(rawArgs));
            var args = rawArgs.Split(' ');
            if (args.Length >= 4 && int.TryParse(args[0], out var channelId))
            {
                var name = $"{args[2]} {args[3]}";
                var worldName = args[1];
                worldName = char.ToUpperInvariant(worldName[0]) + worldName.Substring(1);
                var world = this.Dalamud.Data.GetExcelSheet<World>()
                    .GetRows()
                    .FirstOrDefault((row) => row.Name == worldName);
                if (world == null)
                {
                    PrintLogMessage(LogMessages.WorldNotFound);
                    return null;
                }
                var worldId = world.RowId;
                return new Tuple<string, int, int>(name, worldId, channelId);
            }
            PrintLogMessage(string.Format(CultureInfo.InvariantCulture, LogMessages.TargetUserCommandFailed, command, additionalArgHints));
            return null;
        }
        #endregion

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
            var channelId = int.Parse(command.Substring(command.StartsWith("/global", StringComparison.InvariantCultureIgnoreCase) ? 7 : 3), CultureInfo.InvariantCulture);

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
            if (Config.BlockedUsers.Contains(new UserFragment
            {
                Id = e.Message.AuthorId.ToString(CultureInfo.CurrentCulture),
            }))
                return;
            if (Config.BlockedUsers.Contains(new UserFragment
            {
                Name = e.Message.Author,
                WorldId = e.Message.WorldId,
            }))
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
            => this.Dalamud?.Framework.Gui.Chat.Print(message);

        private static string Capitalize(string input)
            => char.ToUpper(input[0], CultureInfo.CurrentCulture) + input[1..].ToLower(CultureInfo.CurrentCulture);
        
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

                    this.CommandHandler.Dispose();
                    this.ConnectionManager.Dispose();

                    this.http.Dispose();

                    this.Dalamud.Dispose();
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
}
