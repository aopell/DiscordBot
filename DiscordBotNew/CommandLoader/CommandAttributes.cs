using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public class CommandAttribute : Attribute
    {
        public string[] Names { get; }

        public CommandAttribute(params string[] names)
        {
            Names = names.Select(s => s.ToLower()).ToArray();
        }
    }

    public class HelpTextAttribute : Attribute
    {
        public string Text { get; }

        public HelpTextAttribute(string text)
        {
            Text = text;
        }
    }

    public class RequiredAttribute : Attribute
    {

    }

    public class JoinRemainingParametersAttribute : Attribute
    {

    }

    public class CommandScopeAttribute : Attribute
    {
        public ChannelType[] ChannelTypes { get; }

        public CommandScopeAttribute(params ChannelType[] channelTypes)
        {
            ChannelTypes = channelTypes;
        }
    }

    public class PermissionsAttribute : Attribute
    {
        private readonly ChannelPermission[] channelPermissions;
        private readonly GuildPermission[] guildPermissions;
        private readonly bool botOwnerOnly;

        public string GetPermissionError(DiscordMessageContext context)
        {
            context.Bot.Settings.GetSetting("botOwner", out ulong id);
            if (id == context.MessageAuthor.Id) return null;
            if (botOwnerOnly && id != context.MessageAuthor.Id) return "Only the bot owner can run that command";

            var guildChannel = context.Channel as IGuildChannel;
            if (guildChannel == null) return "That command requires permissions that only exist in server channels";
            var missingChannelPermissions = channelPermissions.Where(perm => !((IGuildUser)context.MessageAuthor).GetPermissions((IGuildChannel)context.Channel).Has(perm));
            var missingGuildPermissions = guildPermissions.Where(perm => !((IGuildUser)context.MessageAuthor).GuildPermissions.Has(perm));
            var missingPermissionStrings = missingChannelPermissions.Select(perm => perm.ToString()).Concat(missingGuildPermissions.Select(perm => perm.ToString())).ToArray();
            return missingPermissionStrings.Length > 0 ? $"The following required permissions were missing: {string.Join(", ", missingPermissionStrings)}" : null;
        }

        public PermissionsAttribute(bool ownerOnly = false, ChannelPermission[] channelPermissions = null, GuildPermission[] guildPermissions = null)
        {
            this.channelPermissions = channelPermissions ?? new ChannelPermission[] { };
            this.guildPermissions = guildPermissions ?? new GuildPermission[] { };
            botOwnerOnly = ownerOnly;
        }
    }
}
