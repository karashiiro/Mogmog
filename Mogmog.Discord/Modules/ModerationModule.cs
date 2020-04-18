using Discord;
using Discord.Commands;
using Mogmog.Discord.Services;
using System.Threading.Tasks;

namespace Mogmog.Discord.Modules
{
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        public MogmogConnectionService Mogmog { get; set; }

        [Command("op")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task OpUserAsync(IGuildUser user)
        {
            await Mogmog.OpUserAsync(user);
            await ReplyAsync($"Successfully opped user ${user.Mention}");
        }

        [Command("ban")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanUserAsync(IGuildUser user, [Remainder] string reason = null)
        {
            await Mogmog.BanUserAsync(user);
            await user.BanAsync(reason: reason);
            await Context.User.SendMessageAsync($"Successfully banned user {user} from {user.Guild.Name}." + (reason != null ? $" Reason: {reason}" : string.Empty));
        }

        [Command("unban")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task UnbanUserAsync(IGuildUser user)
        {
            var ban = await Context.Guild.GetBanAsync(user);
            if (ban == null)
            {
                await ReplyAsync("No ban matching the provided user was found.");
                return;
            }

            await Mogmog.UnbanUserAsync(user);
            await Context.Guild.RemoveBanAsync(user);
            await Context.User.SendMessageAsync($"Successfully unbanned user {user} from {user.Guild.Name}.");
        }

        [Command("tempban")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task TempbanUserAsync(IGuildUser user, [Remainder] string parameters = null)
        {
            var endTime = DateTimeUtils.GetDateTime(parameters);
            await Mogmog.TempbanUserAsync(user, endTime);
            await user.BanAsync(reason: $"Should be unbanned at {endTime.ToLongDateString()}");
            await Context.User.SendMessageAsync($"Successfully tempbanned user {user} from {user.Guild.Name} until {endTime.ToLongDateString()}.");
        }

        [Command("kick")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickUserAsync(IGuildUser user, [Remainder] string reason = null)
        {
            await Mogmog.KickUserAsync(user);
            await user.KickAsync(reason: reason);
            await Context.User.SendMessageAsync($"Successfully kicked user {user} from {user.Guild.Name}." + (reason != null ? $" Reason: {reason}" : string.Empty));
        }

        [Command("mute")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task MuteUserAsync(IGuildUser user, [Remainder] string reason = null)
        {
            await Mogmog.MuteUserAsync(user);
            await Context.User.SendMessageAsync($"Successfully muted user {user} in {user.Guild.Name}." + (reason != null ? $" Reason: {reason}" : string.Empty));
        }

        [Command("unmute")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task UnmuteUserAsync(IGuildUser user)
        {
            await Mogmog.UnmuteUserAsync(user);
            await Context.User.SendMessageAsync($"Successfully unmuted user {user} in {user.Guild.Name}.");
        }
    }
}
