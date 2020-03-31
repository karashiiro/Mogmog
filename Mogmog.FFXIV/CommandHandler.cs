using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV
{
    public class CommandHandler : IDisposable
    {
        private readonly DalamudPluginInterface dalamud;
        private readonly Mogmog parent;
        private readonly MogmogConfiguration config;

        private readonly Dictionary<string, CommandInfo> commands;

        public CommandHandler(Mogmog parent, MogmogConfiguration config, DalamudPluginInterface dalamud)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.dalamud = dalamud ?? throw new ArgumentNullException(nameof(dalamud));

            this.commands = new Dictionary<string, CommandInfo>
            {
                { "/mgmgconnect", new CommandInfo(this.parent.AddHost) { HelpMessage = "Connect to a Mogmog server using its address.", ShowInHelp = true, } },
                { "/mgmgdisconnect", new CommandInfo(this.parent.RemoveHost) { HelpMessage = "Disconnect from a Mogmog server using its address.", ShowInHelp = true, } },
            };

            AddCommandHandlers();
        }

        private CommandInfo OnMessageCommandInfo(int i, bool showInHelp = true)
        {
            return new CommandInfo(this.parent.MessageSend)
            {
                HelpMessage = $"Sends a message to the Mogmog global chat channel {i}. Shortcut: /gl{i}",
                ShowInHelp = showInHelp,
            };
        }

        private void AddCommandHandlers()
        {
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                this.dalamud.CommandManager.AddHandler($"/global{i}", OnMessageCommandInfo(i));
                this.dalamud.CommandManager.AddHandler($"/gl{i}", OnMessageCommandInfo(i, false));
            }
            foreach (var command in this.commands)
            {
                this.dalamud.CommandManager.AddHandler(command.Key, command.Value);
            }
        }

        private void RemoveCommandHandlers()
        {
            for (int i = 1; i <= this.config.Hostnames.Count; i++)
            {
                this.dalamud.CommandManager.RemoveHandler($"/global{i}");
                this.dalamud.CommandManager.RemoveHandler($"/gl{i}");
            }
            foreach (var command in this.commands)
            {
                this.dalamud.CommandManager.RemoveHandler(command.Key);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoveCommandHandlers();
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
