﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
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

        private List<KeyValuePair<ulong, int>> orderedUserMessages;

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

        public Leaderboard() { }

        private Leaderboard(ulong guildId)
        {
            TimeGenerated = DateTimeOffset.Now;
            GuildId = guildId;
        }

        public static async Task<Leaderboard> Generate(IGuild guild, DiscordBot bot, LeaderboardType type)
        {
            var leaderboard = new Leaderboard(guild.Id);

            DateTimeOffset today = DateTimeOffset.MinValue;
            DateTimeOffset yesterday = DateTimeOffset.MinValue;

            Leaderboard oldLeaderboard = new Leaderboard();

            if (type == LeaderboardType.Full && bot.Leaderboards.GetSetting(guild.Id.ToString(), out oldLeaderboard))
            {
                leaderboard.OldLeaderboard = oldLeaderboard;
            }
            else if (type == LeaderboardType.Daily || type == LeaderboardType.Today)
            {
                today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time").Date;
                yesterday = today - TimeSpan.FromDays(1);
                oldLeaderboard = new Leaderboard { TimeGenerated = yesterday };
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


                if (type == LeaderboardType.Full)
                {
                    var pages = channel.GetMessagesAsync(int.MaxValue);
                    pages.ForEach(
                    page =>
                    {
                        foreach (IMessage message in page)
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
                }
                else if (type == LeaderboardType.Daily || type == LeaderboardType.Today)
                {
                    oldLeaderboard.ChannelMessages[channel.Id] = 0;

                    List<IMessage> messages = (await channel.GetMessagesAsync().Flatten()).ToList();
                    IMessage lastMessage;
                    do
                    {
                        lastMessage = messages.Last();

                        foreach (var message in messages)
                        {
                            if (message.Timestamp > today)
                            {
                                if (!leaderboard.UserMessages.ContainsKey(message.Author.Id))
                                {
                                    leaderboard.UserMessages.Add(message.Author.Id, 0);
                                    leaderboard.UserLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                                }

                                leaderboard.UserMessages[message.Author.Id]++;
                                messagesInChannel++;
                            }
                            else if (message.Timestamp > yesterday)
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
                    } while (lastMessage.Timestamp > yesterday);
                }

                leaderboard.ChannelMessages[channel.Id] = messagesInChannel;
                leaderboard.TotalMessages += messagesInChannel;
                leaderboard.ChannelLookup.Add(channel.Id, channel.Name);
            }

            if (type == LeaderboardType.Full)
            {
                bot.Leaderboards.AddSetting(guild.Id.ToString(), leaderboard);
                bot.Leaderboards.SaveSettings();
            }

            return leaderboard;
        }

        private double CalculateMessageDifference(ulong id, bool user)
        {
            if (user)
            {
                int current = UserMessages[id];
                return OldLeaderboard.UserMessages.ContainsKey(id) ? current - OldLeaderboard.UserMessages[id] : double.PositiveInfinity;
            }
            else
            {
                int current = ChannelMessages[id];
                return OldLeaderboard.ChannelMessages.ContainsKey(id) ? current - OldLeaderboard.ChannelMessages[id] : double.PositiveInfinity;
            }
        }

        private double CalculatePercentageDifference(ulong id, bool user)
        {
            if (user)
            {
                double current = Math.Round(UserMessages[id] / (float)TotalMessages * 100, 1);
                return OldLeaderboard.UserMessages.ContainsKey(id) ? current - Math.Round(OldLeaderboard.UserMessages[id] / (float)OldLeaderboard.TotalMessages * 100, 1) : float.PositiveInfinity;
            }
            else
            {
                double current = Math.Round(ChannelMessages[id] / (float)TotalMessages * 100, 1);
                return OldLeaderboard.ChannelMessages.ContainsKey(id) ? current - Math.Round(OldLeaderboard.ChannelMessages[id] / (float)OldLeaderboard.TotalMessages * 100) : float.PositiveInfinity;
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

            return oldIndex < 0 ? '*' : (oldIndex < index ? '▼' : (oldIndex == index ? '-' : '▲'));
        }

        public override string ToString()
        {
            var builder = new StringBuilder("**Messages Leaderboard**\n```\n");
            builder.AppendLine("Channels");
            foreach (var channel in OrderedChannelMessages)
            {
                if (OldLeaderboard == null)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   #{2}\n", channel.Value, channel.Value / (float)TotalMessages * 100, ChannelLookup[channel.Key]);
                }
                else
                {
                    builder.AppendFormat("{5}  {0,-7} ({3:+;-;+}{3,4:#;#;0}) {1,8:0.0}% ({4:+;-;+}{4,4:0.0;0.0;0.0}%)   #{2}\n", channel.Value, channel.Value / (float)TotalMessages * 100, ChannelLookup[channel.Key], CalculateMessageDifference(channel.Key, false), CalculatePercentageDifference(channel.Key, false), GetDifferenceChar(channel.Key, false));
                }
            }
            builder.AppendLine("\nUsers");
            foreach (var user in OrderedUserMessages)
            {
                if (OldLeaderboard == null)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   {2}\n", user.Value, user.Value / (float)TotalMessages * 100, UserLookup[user.Key]);
                }
                else
                {
                    builder.AppendFormat("{5}  {0,-7} ({3:+;-;+}{3,4:#;#;0}) {1,8:0.0}% ({4:+;-;+}{4,4:0.0;0.0;0.0}%)   {2}\n", user.Value, user.Value / (float)TotalMessages * 100, UserLookup[user.Key], CalculateMessageDifference(user.Key, true), CalculatePercentageDifference(user.Key, true), GetDifferenceChar(user.Key, true));
                }
            }
            if (OldLeaderboard == null)
            {
                builder.AppendLine($"\nTotal messages in server: {TotalMessages}");
            }
            else
            {
                builder.AppendLine($"\nTotal messages in server: {TotalMessages} ({TotalMessages - OldLeaderboard.TotalMessages:+#;-#;+0})");
                builder.AppendLine($"Changes from {(TimeGenerated - OldLeaderboard.TimeGenerated).ToLongString()} ago");
            }
            builder.Append("```");

            return builder.ToString();
        }
    }

    public enum LeaderboardType
    {
        Full,
        Today,
        Daily = 1
    }
}