using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public static async Task<string> ReplaceAsync(this Regex regex, string input, Func<Match, Task<string>> replacementFn)
        {
            var sb = new StringBuilder();
            var lastIndex = 0;

            foreach (Match match in regex.Matches(input))
            {
                sb.Append(input, lastIndex, match.Index - lastIndex)
                  .Append(await replacementFn(match).ConfigureAwait(false));

                lastIndex = match.Index + match.Length;
            }

            sb.Append(input, lastIndex, input.Length - lastIndex);
            return sb.ToString();
        }

        public static string GetCommandPrefix(ICommandContext context, ISocketMessageChannel channel)
        {
            context.Bot.Settings.GetSetting("commandPrefix", out string commandPrefix);
            commandPrefix = commandPrefix ?? "!";

            if (context.Bot.Settings.GetSetting("customPrefixes", out Dictionary<ulong, string> prefixes))
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
        public static string AvatarUrlOrDefaultAvatar(this IUser user) => user.GetAvatarUrl() ?? "https://discordapp.com/assets/6debd47ed13483642cf09e832ed0bc1b.png";

        public static async Task<IUser> GetUserByUsername(string username, ISocketMessageChannel channel)
        {
            var users = (await channel.GetUsersAsync().Flatten()).Where(user => user.Username == username).ToArray();
            if (users.Length == 1)
                return users[0];
            if (users.Length > 0)
                throw new ArgumentException($"Multiple users were found with the username {username}");
            throw new ArgumentException($"No user was found with the username {username} in this channel");
        }

        public static string ToLongString(this TimeSpan difference)
        {
            var response = new StringBuilder();
            response.Append(difference.Days != 0 ? $"{difference.Days} day{(difference.Days == 1 ? "" : "s")} " : "");
            response.Append(difference.TotalHours >= 1 ? $"{difference.Hours} hour{(difference.Hours == 1 ? "" : "s")} " : "");
            response.Append(difference.TotalMinutes >= 1 ? $"{difference.Minutes} minute{(difference.Minutes == 1 ? "" : "s")} " : "");
            response.Append($"{difference.Seconds} second{(difference.Seconds == 1 ? "" : "s")}");
            return response.ToString();
        }
    }
}
