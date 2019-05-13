using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader.CommandContext;

namespace DiscordBotNew.CommandLoader
{
    public static class CommandTools
    {
        public static Emote LoadingEmote => Emote.Parse("<:dualring:395760275992739862>");

        public static DateTimeOffset? ParseDate(this TimeZoneInfo timezone, string dateTime)
        {
            if (DateTimeOffset.TryParse(dateTime, out DateTimeOffset parsedDateLocal))
            {
                // must use .DateTime, otherwise timezone does not infer that the passed date is given in its own TZ
                var tzOffset = timezone.GetUtcOffset(parsedDateLocal.DateTime);
                var parsedDateTimeZone = new DateTimeOffset(parsedDateLocal.DateTime, tzOffset);
                return parsedDateTimeZone;
            }

            return null;
        }

        public static ChannelType GetChannelType(this IMessage m)
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

        public static ChannelType GetChannelType(this IMessageChannel m)
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

        public static string GetCommandPrefix(ICommandContext context, IMessageChannel channel)
        {
            string commandPrefix = context.Bot.Settings.CommandPrefix ?? "!";

            if (context.Bot.Settings.CustomPrefixes != null)
            {
                if (channel.GetChannelType() == ChannelType.Text)
                {
                    var guildChannel = channel as IGuildChannel;
                    return guildChannel == null || !context.Bot.Settings.CustomPrefixes.ContainsKey(guildChannel.Guild.Id) ? commandPrefix : context.Bot.Settings.CustomPrefixes[guildChannel.Guild.Id];
                }

                return context.Bot.Settings.CustomPrefixes.ContainsKey(channel.Id) ? context.Bot.Settings.CustomPrefixes[channel.Id] : commandPrefix;
            }

            return commandPrefix;
        }

        public static string NicknameOrUsername(this IUser user) => (user as IGuildUser)?.Nickname ?? user.Username;
        public static string AvatarUrlOrDefaultAvatar(this IUser user) => user.GetAvatarUrl() ?? $"https://cdn.discordapp.com/embed/avatars/{user.DiscriminatorValue % 5}.png";

        public static async Task<IUser> GetUserByUsername(string username, IMessageChannel channel)
        {
            var users = (await channel.GetUsersAsync().FlattenAsync()).Where(user => user.Username == username).ToArray();
            if (users.Length == 1)
                return users[0];
            if (users.Length > 0)
                throw new ArgumentException($"Multiple users were found with the username {username}");
            throw new ArgumentException($"No user was found with the username {username} in this channel");
        }

        public static string ToLongString(this TimeSpan difference, bool showSeconds = true, bool showZeroValues = false)
        {
            var response = new StringBuilder();
            if (showZeroValues)
            {
                response.Append(difference.Days != 0 ? $"{difference.Days} day{(difference.Days == 1 ? "" : "s")} " : "");
                response.Append(difference.TotalHours >= 1 ? $"{difference.Hours} hour{(difference.Hours == 1 ? "" : "s")} " : "");
                response.Append(difference.TotalMinutes >= 1 ? $"{difference.Minutes} minute{(difference.Minutes == 1 ? "" : "s")} " : "");
                response.Append(showSeconds ? $"{difference.Seconds} second{(difference.Seconds == 1 ? "" : "s")}" : "");
            }
            else
            {
                response.Append(difference.Days != 0 ? $"{difference.Days} day{(difference.Days == 1 ? "" : "s")} " : "");
                response.Append(difference.Hours != 0 ? $"{difference.Hours} hour{(difference.Hours == 1 ? "" : "s")} " : "");
                response.Append(difference.Minutes != 0 ? $"{difference.Minutes} minute{(difference.Minutes == 1 ? "" : "s")} " : "");
                response.Append(showSeconds && difference.Seconds != 0 ? $"{difference.Seconds} second{(difference.Seconds == 1 ? "" : "s")}" : "");
            }

            return response.ToString().Trim();
        }

        public static string ToShortString(this TimeSpan difference, bool showSeconds = true, bool showZeroValues = false)
        {
            var response = new StringBuilder();
            if (showZeroValues)
            {
                response.Append(difference.Days != 0 ? $"{difference.Days}d " : "");
                response.Append(difference.TotalHours >= 1 ? $"{difference.Hours}h " : "");
                response.Append(difference.TotalMinutes >= 1 ? $"{difference.Minutes}m " : "");
                response.Append(showSeconds ? $"{difference.Seconds}s" : "");
            }
            else
            {
                response.Append(difference.Days != 0 ? $"{difference.Days}d " : "");
                response.Append(difference.Hours != 0 ? $"{difference.Hours}h " : "");
                response.Append(difference.Minutes != 0 ? $"{difference.Minutes}m " : "");
                response.Append(showSeconds && difference.Seconds != 0 ? $"{difference.Seconds}s" : "");
            }

            return response.ToString().Trim();
        }

        public static TValue GetOrAddDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        {
            if (!dictionary.ContainsKey(key))
            {
                if (typeof(TValue).IsValueType)
                {
                    dictionary.Add(key, default(TValue));
                }
                else
                {
                    try
                    {
                        dictionary.Add(key, Activator.CreateInstance<TValue>());
                    }
                    catch
                    {
                        dictionary.Add(key, default(TValue));
                    }
                }
            }

            return dictionary[key];
        }

        public static void UpdateOrAddDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue newValue)
        {
            if (!dictionary.ContainsKey(key))
            {
                if (typeof(TValue).IsValueType)
                {
                    dictionary.Add(key, default(TValue));
                }
                else
                {
                    try
                    {
                        dictionary.Add(key, Activator.CreateInstance<TValue>());
                    }
                    catch
                    {
                        dictionary.Add(key, default(TValue));
                    }
                }
            }

            dictionary[key] = newValue;
        }

        public static string[] ParseArguments(string input)
        {
            var args = new List<string>();
            var currentString = new StringBuilder();
            bool insideQuote = false;
            for (int i = 0; i < input.Length; i++)
            {
                if (!insideQuote && input[i] == ' ')
                {
                    args.Add(currentString.ToString());
                    currentString.Clear();
                }
                else if (input[i] == '"')
                {
                    if (insideQuote)
                    {
                        args.Add(currentString.ToString());
                        currentString.Clear();
                        insideQuote = false;
                        if (i < input.Length - 1 && input[i + 1] == ' ')
                        {
                            i++;
                        }
                    }
                    else
                    {
                        insideQuote = true;
                    }
                }
                else if (input[i] == '\\' && i < input.Length - 1)
                {
                    switch (input[i + 1])
                    {
                        case '\\':
                            currentString.Append('\\');
                            i++;
                            break;
                        case '"':
                            currentString.Append('"');
                            i++;
                            break;
                        default:
                            currentString.Append('\\');
                            break;
                    }
                }
                else
                {
                    currentString.Append(input[i]);
                }
            }

            if (!string.IsNullOrWhiteSpace(currentString.ToString()))
            {
                args.Add(currentString.ToString());
            }

            return args.ToArray();
        }

        public static EmbedBuilder GenerateCountdownEmbed(DiscordBot bot, string name, DateTimeOffset date)
        {
            var now = DateTimeOffset.Now;
            TimeSpan difference = date.ToUniversalTime() - now;
            var then = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now + difference, bot.DefaultTimeZone);
            return new EmbedBuilder().WithDescription($"in {difference.ToLongString()}")
                                     .WithAuthor(name, "https://emojipedia-us.s3.amazonaws.com/thumbs/120/twitter/120/stopwatch_23f1.png")
                                     .WithColor(0x7689d8)
                                     .AddField("🗓", then.ToString("dddd, MMMM d, yyyy"), true)
                                     .AddField(GetClockEmoji(then), then.ToString("t"), true);
        }

        public static EmbedBuilder GenerateCountdownCompleteEmbed(DiscordBot bot, string name, DateTimeOffset date)
        {
            var now = DateTimeOffset.Now;
            TimeSpan difference = date.ToUniversalTime() - now;
            var then = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now + difference, bot.DefaultTimeZone);
            return new EmbedBuilder().WithTitle("Countdown Complete")
                                     .WithDescription(name)
                                     .WithThumbnailUrl("https://emojipedia-us.s3.amazonaws.com/thumbs/120/twitter/120/stopwatch_23f1.png")
                                     .WithColor(0xDE2A42)
                                     .AddField("🗓", then.ToString("dddd, MMMM d, yyyy"), true)
                                     .AddField(GetClockEmoji(then), then.ToString("t"), true);
        }

        private static string GetClockEmoji(DateTimeOffset time)
        {
            string[] clocks = { "🕛", "🕧", "🕐", "🕜", "🕑", "🕝", "🕒", "🕞", "🕓", "🕟", "🕔", "🕠", "🕕", "🕡", "🕖", "🕢", "🕗", "🕣", "🕘", "🕤", "🕙", "🕥", "🕚", "🕦" };
            return clocks[time.Hour % 12 * 2 + (time.Minute < 30 ? 0 : 1)];
        }

        public static bool HasChannelPermissions(this DiscordUserMessageContext context, params ChannelPermission[] perms)
        {
            var existing = ((IGuildUser) context.MessageAuthor).GetPermissions(context.Channel as IGuildChannel);
            return perms.All(p => existing.Has(p));
        }

        public static bool HasGuildPermissions(this DiscordUserMessageContext context, params GuildPermission[] perms)
        {
            var existing = ((IGuildUser) context.MessageAuthor).GuildPermissions;
            return perms.All(p => existing.Has(p));
        }
    }
}
