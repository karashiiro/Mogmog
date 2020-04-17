using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using static Dalamud.Game.Command.CommandInfo;

namespace Mogmog.FFXIV
{
    public class CommandHandler : IChatCommandHandler, IDisposable
    {
        private readonly DalamudPluginInterface dalamud;
        private readonly MogmogPlugin parent;
        private readonly MogmogConfiguration config;

        private readonly Dictionary<string, CommandInfo> commands;

        public CommandHandler(MogmogPlugin parent, MogmogConfiguration config, DalamudPluginInterface dalamud)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.dalamud = dalamud ?? throw new ArgumentNullException(nameof(dalamud));

            this.commands = new Dictionary<string, CommandInfo>();
            AddCommand("/mgadd", this.parent.AddHost, CommandInfoMessages.Add);
            AddCommand("/mgremove", this.parent.RemoveHost, CommandInfoMessages.Remove);
            AddCommand("/mgreload", this.parent.ReloadHost, CommandInfoMessages.Reload);
            AddCommand("/mgban", this.parent.BanUser, CommandInfoMessages.Ban + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgban"));
            AddCommand("/mgunban", this.parent.UnbanUser, CommandInfoMessages.Unban + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgunban"));
            AddCommand("/mgtempban", this.parent.TempbanUser, CommandInfoMessages.Tempban + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgtempban"));
            AddCommand("/mgkick", this.parent.KickUser, CommandInfoMessages.Kick + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgkick"));
            AddCommand("/mgmute", this.parent.MuteUser, CommandInfoMessages.Mute + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgmute"));
            AddCommand("/mgunmute", this.parent.UnmuteUser, CommandInfoMessages.Unmute + " " + string.Format(CultureInfo.InvariantCulture, CommandInfoMessages.TargetUserUsage, "/mgunmute"));
            AddChatHandlers();
        }

        public void AddChatHandler(int i)
        {
            this.dalamud.CommandManager.AddHandler($"/global{i}", OnMessageCommandInfo(i));
            this.dalamud.CommandManager.AddHandler($"/gl{i}", OnMessageCommandInfo(i, false));
        }

        public void RemoveChatHandler(int i)
        {
            this.dalamud.CommandManager.RemoveHandler($"/global{i}");
            this.dalamud.CommandManager.RemoveHandler($"/gl{i}");
        }

        private CommandInfo OnMessageCommandInfo(int i, bool showInHelp = true)
        {
            return new CommandInfo(this.parent.MessageSend)
            {
                HelpMessage = $"Sends a message to the Mogmog global chat channel {i}. Shortcut: /gl{i}",
                ShowInHelp = showInHelp,
            };
        }

        private void AddChatHandlers()
        {
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                AddChatHandler(i);
            }
            foreach (var command in this.commands)
            {
                this.dalamud.CommandManager.AddHandler(command.Key, command.Value);
            }
        }

        private void RemoveChatHandlers()
        {
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                RemoveChatHandler(i);
            }
            foreach (var command in this.commands)
            {
                this.dalamud.CommandManager.RemoveHandler(command.Key);
            }
        }

        private void AddCommand(string command, HandlerDelegate function, string helpMessage)
        {
            this.commands.Add(
                command, new CommandInfo(function) { HelpMessage = helpMessage, ShowInHelp = true, }
            );
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoveChatHandlers();
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
