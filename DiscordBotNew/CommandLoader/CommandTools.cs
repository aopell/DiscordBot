using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public static class CommandTools
    {
        public static async Task ReplyError(this SocketMessage m, string description, string title = "Error")
        {

        }

        public static async Task ReplyError(this SocketMessage m, Exception error) => await m.ReplyError(error.Message, $"Error - {error.GetType().Name}");

        public static ChannelType GetChannelType(this SocketMessage m)
        {
            switch (m.Channel)
            {
                case IGroupChannel _:
                    return ChannelType.Group;
                case IDMChannel _:
                    return ChannelType.DM;
                default:
                    return ChannelType.Text;
            }
        }

        public static ChannelType GetChannelType(this ISocketMessageChannel m)
        {
            switch (m)
            {
                case IGroupChannel _:
                    return ChannelType.Group;
                case IDMChannel _:
                    return ChannelType.DM;
                default:
                    return ChannelType.Text;
            }
        }

        public static string GetCommandPrefix(ISocketMessageChannel channel)
        {
            SettingsManager.GetSetting("commandPrefix", out string commandPrefix);
            commandPrefix = commandPrefix ?? "!";

            if (SettingsManager.GetSetting("customPrefixes", out Dictionary<ulong, string> prefixes))
            {
                if (channel.GetChannelType() == ChannelType.Text)
                {
                    var guildChannel = channel as IGuildChannel;
                    return guildChannel == null || !prefixes.ContainsKey(guildChannel.Guild.Id) ? commandPrefix : prefixes[guildChannel.Guild.Id];
                }
                return prefixes.ContainsKey(channel.Id) ? prefixes[channel.Id] : commandPrefix;
            }

            return commandPrefix;
        }

        public static string NicknameOrUsername(this IUser user) => (user as IGuildUser)?.Nickname ?? user.Username;
    }
}
