using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew.Commands
{
    public class Leaderboard
    {
        public ulong GuildId { get; set; }
        public int TotalMessages { get; set; }
        public Dictionary<ulong, int> ChannelMessages { get; set; } = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> UserMessages { get; set; } = new Dictionary<ulong, int>();
        public DateTimeOffset TimeGenerated { get; set; }

        private LeaderboardType Type { get; }
        private TimeSpan TimePeriod { get; }

        private List<KeyValuePair<ulong, int>> orderedUserMessages;

        private DiscordBot bot;

        private List<KeyValuePair<ulong, int>> OrderedUserMessages
        {
            get
            {
                orderedUserMessages = orderedUserMessages ?? UserMessages.OrderByDescending(x => x.Value).ToList();
                return orderedUserMessages;
            }
        }

        private List<KeyValuePair<ulong, int>> orderedChannelMessages;
        private List<KeyValuePair<ulong, int>> OrderedChannelMessages
        {
            get
            {
                orderedChannelMessages = orderedChannelMessages ?? ChannelMessages.OrderByDescending(x => x.Value).ToList();
                return orderedChannelMessages;
            }
        }

        private Dictionary<ulong, string> ChannelLookup { get; set; } = new Dictionary<ulong, string>();
        private Dictionary<ulong, string> UserLookup { get; set; } = new Dictionary<ulong, string>();

        private Leaderboard OldLeaderboard { get; set; }

        public Leaderboard()
        {
            Type = LeaderboardType.Full;
        }

        private Leaderboard(ulong guildId, LeaderboardType type, DiscordBot bot, DateTimeOffset creationTime, TimeSpan timePeriod = default(TimeSpan))
        {
            TimeGenerated = creationTime;
            GuildId = guildId;
            Type = type;
            TimePeriod = timePeriod;
            this.bot = bot;
        }

        public static async Task<Leaderboard> GenerateFullLeaderboard(IGuild guild, DiscordBot bot, DateTimeOffset creationTime)
        {
            var leaderboard = new Leaderboard(guild.Id, LeaderboardType.Full, bot, creationTime);

            if (bot.Leaderboards.GetSetting(guild.Id.ToString(), out Leaderboard oldLeaderboard))
            {
                leaderboard.OldLeaderboard = oldLeaderboard;
            }

            var channels = await guild.GetTextChannelsAsync();

            foreach (ITextChannel channel in channels)
            {
                ChannelPermissions permissions = (await guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (!permissions.ReadMessages || !permissions.ReadMessageHistory)
                {
                    continue;
                }

                int messagesInChannel = 0;

                var pages = channel.GetMessagesAsync(int.MaxValue);
                pages.ForEach(
                page =>
                {
                    foreach (IMessage message in page.Where(message => message.Timestamp <= creationTime))
                    {
                        if (!leaderboard.UserMessages.ContainsKey(message.Author.Id))
                        {
                            leaderboard.UserMessages.Add(message.Author.Id, 0);
                            leaderboard.UserLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                        }

                        leaderboard.UserMessages[message.Author.Id]++;
                        messagesInChannel++;
                    }
                });

                leaderboard.ChannelMessages[channel.Id] = messagesInChannel;
                leaderboard.TotalMessages += messagesInChannel;
                leaderboard.ChannelLookup.Add(channel.Id, channel.Name);
            }

            bot.Leaderboards.AddSetting(guild.Id.ToString(), leaderboard);
            bot.Leaderboards.SaveSettings();

            return leaderboard;
        }

        public static async Task<Leaderboard> GenerateTimeBasedLeaderboard(IGuild guild, DiscordBot bot, LeaderboardType type, DateTimeOffset creationTime, double hours = 24d)
        {
            var leaderboard = new Leaderboard(guild.Id, type, bot, creationTime, TimeSpan.FromHours(hours));

            Leaderboard oldLeaderboard = new Leaderboard();
            DateTimeOffset startTime = DateTimeOffset.MinValue;
            DateTimeOffset deltaTime = DateTimeOffset.MinValue;

            if (type == LeaderboardType.Today)
            {
                // I'm sure this isn't the right way to do this but quite honestly I was getting annoyed and this works, so ¯\_(ツ)_/¯
                TimeSpan offset = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, bot.DefaultTimeZone).Offset;
                startTime = new DateTimeOffset(new DateTimeOffset(DateTime.UtcNow.Ticks + TimeSpan.FromHours(offset.Hours).Ticks, offset).Date, offset);
            }
            else if (type == LeaderboardType.Past24Hours)
            {
                startTime = DateTimeOffset.Now - TimeSpan.FromDays(1);
            }
            else if (type == LeaderboardType.Custom)
            {
                startTime = DateTimeOffset.Now - TimeSpan.FromHours(hours);
            }

            deltaTime = startTime - TimeSpan.FromHours(hours);
            oldLeaderboard = new Leaderboard { TimeGenerated = deltaTime };
            leaderboard.OldLeaderboard = oldLeaderboard;

            var channels = await guild.GetTextChannelsAsync();

            foreach (ITextChannel channel in channels)
            {
                ChannelPermissions permissions = (await guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (!permissions.ReadMessages || !permissions.ReadMessageHistory)
                {
                    continue;
                }

                int messagesInChannel = 0;
                oldLeaderboard.ChannelMessages[channel.Id] = 0;

                var messages = (await channel.GetMessagesAsync(limit: 100).Flatten()).ToList();
                IMessage lastMessage;
                do
                {
                    lastMessage = messages.LastOrDefault();

                    if (lastMessage == null) break;

                    foreach (var message in messages)
                    {
                        if (message.Timestamp > startTime && message.Timestamp <= creationTime)
                        {
                            if (!leaderboard.UserMessages.ContainsKey(message.Author.Id))
                            {
                                leaderboard.UserMessages.Add(message.Author.Id, 0);
                                leaderboard.UserLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                            }

                            leaderboard.UserMessages[message.Author.Id]++;
                            messagesInChannel++;
                        }
                        else if (message.Timestamp > deltaTime && message.Timestamp <= creationTime)
                        {
                            if (!oldLeaderboard.UserMessages.ContainsKey(message.Author.Id))
                            {
                                oldLeaderboard.UserMessages.Add(message.Author.Id, 0);
                                oldLeaderboard.UserLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                            }

                            oldLeaderboard.UserMessages[message.Author.Id]++;
                            oldLeaderboard.ChannelMessages[channel.Id]++;
                            oldLeaderboard.TotalMessages++;
                        }
                    }

                    messages = (await channel.GetMessagesAsync(lastMessage, Direction.Before).Flatten()).ToList();
                } while (lastMessage.Timestamp > deltaTime);

                leaderboard.ChannelMessages[channel.Id] = messagesInChannel;
                leaderboard.TotalMessages += messagesInChannel;
                leaderboard.ChannelLookup.Add(channel.Id, channel.Name);
            }
            return leaderboard;
        }

        public static async Task<Leaderboard> GenerateDeltaLeaderboard(IGuild guild, DiscordBot bot, DateTimeOffset creationTime)
        {
            var leaderboard = new Leaderboard(guild.Id, LeaderboardType.Delta, bot, creationTime);

            if (bot.Leaderboards.GetSetting(guild.Id.ToString(), out Leaderboard oldLeaderboard))
            {
                leaderboard.OldLeaderboard = oldLeaderboard;
            }
            else
            {
                return await GenerateFullLeaderboard(guild, bot, creationTime);
            }

            leaderboard.ChannelMessages = leaderboard.OldLeaderboard.ChannelMessages.ToDictionary(x => x.Key, x => x.Value);
            leaderboard.UserMessages = leaderboard.OldLeaderboard.UserMessages.ToDictionary(x => x.Key, x => x.Value);
            leaderboard.TotalMessages = leaderboard.OldLeaderboard.TotalMessages;


            var channels = await guild.GetTextChannelsAsync();

            foreach (ITextChannel channel in channels)
            {
                ChannelPermissions permissions = (await guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (!permissions.ReadMessages || !permissions.ReadMessageHistory)
                {
                    continue;
                }

                int messagesInChannel = 0;

                var messages = (await channel.GetMessagesAsync(limit: 100).Flatten()).ToList();
                IMessage lastMessage;
                do
                {
                    lastMessage = messages.LastOrDefault();

                    if (lastMessage == null) break;

                    foreach (IMessage message in messages.Where(message => message.Timestamp > leaderboard.OldLeaderboard.TimeGenerated && message.Timestamp <= creationTime))
                    {
                        if (!leaderboard.UserMessages.ContainsKey(message.Author.Id))
                        {
                            leaderboard.UserMessages.Add(message.Author.Id, 0);
                        }
                        if (!leaderboard.UserLookup.ContainsKey(message.Author.Id))
                        {
                            leaderboard.UserLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                        }

                        leaderboard.UserMessages[message.Author.Id]++;
                        messagesInChannel++;
                    }

                    messages = (await channel.GetMessagesAsync(lastMessage, Direction.Before).Flatten()).ToList();
                } while (lastMessage.Timestamp > leaderboard.OldLeaderboard.TimeGenerated);

                if (!leaderboard.ChannelMessages.ContainsKey(channel.Id))
                {
                    leaderboard.ChannelMessages.Add(channel.Id, messagesInChannel);
                }
                else
                {
                    leaderboard.ChannelMessages[channel.Id] += messagesInChannel;
                }
                leaderboard.TotalMessages += messagesInChannel;
                if (!leaderboard.ChannelLookup.ContainsKey(channel.Id))
                {
                    leaderboard.ChannelLookup.Add(channel.Id, channel.Name);
                }
            }

            bot.Leaderboards.AddSetting(guild.Id.ToString(), leaderboard);
            bot.Leaderboards.SaveSettings();

            return leaderboard;
        }

        private int CalculateMessageDifference(ulong id, bool user)
        {
            if (user)
            {
                int current = UserMessages[id];
                return OldLeaderboard.UserMessages.ContainsKey(id) ? current - OldLeaderboard.UserMessages[id] : current;
            }
            else
            {
                int current = ChannelMessages[id];
                return OldLeaderboard.ChannelMessages.ContainsKey(id) ? current - OldLeaderboard.ChannelMessages[id] : current;
            }
        }

        private double CalculatePercentageDifference(ulong id, bool user)
        {
            if (user)
            {
                double current = UserMessages[id] / (double)TotalMessages;
                return OldLeaderboard.UserMessages.ContainsKey(id) ? current - (OldLeaderboard.UserMessages[id] / (double)OldLeaderboard.TotalMessages) : current;
            }
            else
            {
                double current = ChannelMessages[id] / (double)TotalMessages;
                return OldLeaderboard.ChannelMessages.ContainsKey(id) ? current - (OldLeaderboard.ChannelMessages[id] / (double)OldLeaderboard.TotalMessages) : current;
            }
        }

        private char GetDifferenceChar(ulong id, bool user)
        {
            int index;
            int oldIndex;
            if (user)
            {
                index = OrderedUserMessages.FindIndex(x => x.Key == id);
                oldIndex = OldLeaderboard.OrderedUserMessages.FindIndex(x => x.Key == id);
            }
            else
            {
                index = OrderedChannelMessages.FindIndex(x => x.Key == id);
                oldIndex = OldLeaderboard.OrderedChannelMessages.FindIndex(x => x.Key == id);
            }

            return oldIndex < 0 ? '!' : (oldIndex < index ? '-' : (oldIndex == index ? '~' : '+'));
        }

        public override string ToString() => string.Join("\n", ToStringsAsync().Result);

        public async Task<List<string>> ToStringsAsync(bool combine = false)
        {
            IGuild guild = bot.Client.GetGuild(GuildId);
            List<string> messages = new List<string>();

            var builder = new StringBuilder($"**Messages Leaderboard**\n");
            switch (Type)
            {
                case LeaderboardType.Full:
                case LeaderboardType.Delta:
                    builder.AppendLine("For messages sent from the beginning of time");
                    break;
                case LeaderboardType.Today:
                    builder.AppendLine("For messages since midnight PT");
                    break;
                case LeaderboardType.Custom:
                case LeaderboardType.Past24Hours:
                    builder.AppendLine($"For messages in the last {TimePeriod.ToLongString()}");
                    break;
            }

            builder.AppendLine($"```diff\n{(OrderedChannelMessages.Count(channel => ChannelLookup.ContainsKey(channel.Key)) > 25 ? "Top 25 " : "")}Channels");
            foreach (var channel in OrderedChannelMessages.Where(channel => ChannelLookup.ContainsKey(channel.Key)).Take(25))
            {
                if (OldLeaderboard == null)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   #{2}\n", channel.Value, channel.Value / (double)TotalMessages * 100, ChannelLookup.TryGetValue(channel.Key, out string channelName) ? channelName : "<deleted channel>");
                }
                else
                {
                    builder.AppendFormat("{5}  {0,-7} ({3:+;-}{3,4:###0;###0}) {1,9:0.00%} ({4,7:+00.00%;-00.00%})   #{2}\n", channel.Value, channel.Value / (double)TotalMessages, ChannelLookup.TryGetValue(channel.Key, out string channelName) ? channelName : "<deleted channel>", CalculateMessageDifference(channel.Key, false), CalculatePercentageDifference(channel.Key, false), GetDifferenceChar(channel.Key, false));
                }
            }
            if (!combine)
            {
                builder.AppendLine("```");
                messages.Add(builder.ToString());
                builder.Clear();
                builder.AppendLine("```diff");
            }
            builder.AppendLine($"\n{(OrderedUserMessages.Count > 25 ? "Top 25 " : "")}Users");
            foreach (var user in OrderedUserMessages.Take(25))
            {
                if (OldLeaderboard == null)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   {2}\n", user.Value, user.Value / (double)TotalMessages * 100, UserLookup.TryGetValue(user.Key, out string username) ? username : (await guild.GetUserAsync(user.Key))?.NicknameOrUsername() ?? (await bot.RestClient.GetUserAsync(user.Key))?.Username ?? "<unknown user>");
                }
                else
                {
                    builder.AppendFormat("{5}  {0,-7} ({3:+;-}{3,4:###0;###0}) {1,9:0.00%} ({4,7:+00.00%;-00.00%})   {2}\n", user.Value, user.Value / (double)TotalMessages, UserLookup.TryGetValue(user.Key, out string username) ? username.Replace("```", "`​`​`​") : (await guild.GetUserAsync(user.Key))?.NicknameOrUsername().Replace("```", "`​`​`​") ?? (await bot.RestClient.GetUserAsync(user.Key))?.Username.Replace("```", "`​`​`​") ?? "<unknown user>", CalculateMessageDifference(user.Key, true), CalculatePercentageDifference(user.Key, true), GetDifferenceChar(user.Key, true));
                }
            }
            if (OldLeaderboard == null)
            {
                builder.AppendLine($"\nTotal messages in server: {TotalMessages}");
            }
            else
            {
                switch (Type)
                {
                    case LeaderboardType.Full:
                    case LeaderboardType.Delta:
                        builder.AppendLine($"\nTotal messages in server: {TotalMessages} ({TotalMessages - OldLeaderboard.TotalMessages:+#;-#;+0})\n");
                        builder.AppendLine($"Changes from {(TimeGenerated - OldLeaderboard.TimeGenerated).ToLongString()} ago");
                        break;
                    case LeaderboardType.Today:
                        builder.AppendLine($"\nTotal messages sent today: {TotalMessages} ({TotalMessages - OldLeaderboard.TotalMessages:+#;-#;+0})\n");
                        builder.AppendLine("All current values since midnight PT, delta values are comparisons from the previous day");
                        break;
                    case LeaderboardType.Custom:
                    case LeaderboardType.Past24Hours:
                        builder.AppendLine($"\nTotal messages sent in the last {TimePeriod.ToLongString()}: {TotalMessages} ({TotalMessages - OldLeaderboard.TotalMessages:+#;-#;+0})\n");
                        builder.AppendLine($"All current values since {TimePeriod.ToLongString()} ago, delta values are comparisons from the previous {TimePeriod.ToLongString()}");
                        break;
                }
            }
            builder.AppendLine($"Generated {TimeZoneInfo.ConvertTimeBySystemTimeZoneId(TimeGenerated, bot.DefaultTimeZone):f}");
            builder.Append("```");
            messages.Add(builder.ToString());

            return messages;
        }

        public async Task<string> ToStringAsync() => (await ToStringsAsync(combine: true)).First();
    }

    public enum LeaderboardType
    {
        [HelpText("Counts every message sent in the server")] Full,
        [HelpText("Counts messages sent since midnight PT")] Today,
        [HelpText("Counts messages sent in the last 24 houts")] Past24Hours,
        [HelpText("Counts messages since the last leaderboard and adds them to the previous total")] Delta,
        [HelpText("Counts messages sent in the last specified number of hours")] Custom
    }
}
