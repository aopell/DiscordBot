using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;
using DiscordBotNew.Settings;
using Newtonsoft.Json;

namespace DiscordBotNew.Commands
{
    public static class AnalyticsCommands
    {
        [Command("leaderboard"), HelpText("Counts messages sent by each person in a server"), CommandScope(ChannelType.Text)]
        [Permissions(guildPermissions: new[] { GuildPermission.ReadMessageHistory, GuildPermission.ManageMessages, GuildPermission.ReadMessages }), ChannelDescriptionDelay(21600)]
        public static async Task<ICommandResult> GenerateLeaderboard(ICommandContext context, LeaderboardType type = LeaderboardType.Delta, [HelpText("Specifies the time frame for custom leaderboards")] double customHours = 24d)
        {
            IGuild guild;
            ITextChannel messageChannel;
            DateTimeOffset timestamp;
            switch (context)
            {
                case DiscordMessageContext d:
                    guild = d.Guild;
                    messageChannel = (ITextChannel)d.Channel;
                    timestamp = d.Message.Timestamp;
                    break;
                case DiscordChannelDescriptionContext d:
                    guild = d.Channel.Guild;
                    messageChannel = (ITextChannel)d.Channel;
                    timestamp = DateTimeOffset.Now;
                    break;
                default:
                    return new ErrorResult($"The `leaderboard` command is not valid in the context `{context.GetType().Name}`");
            }

            if (context is DiscordUserMessageContext)
            {
                await messageChannel.SendMessageAsync("Calculating messages sent. This may take a few seconds...");
            }

            using (messageChannel.EnterTypingState())
            {
                Leaderboard leaderboard;
                switch (type)
                {
                    case LeaderboardType.Full:
                        leaderboard = await Leaderboard.GenerateFullLeaderboard(guild, context.Bot, timestamp);
                        break;
                    case LeaderboardType.Today:
                    case LeaderboardType.Past24Hours:
                        leaderboard = await Leaderboard.GenerateTimeBasedLeaderboard(guild, context.Bot, type, timestamp);
                        break;
                    case LeaderboardType.Delta:
                        leaderboard = await Leaderboard.GenerateDeltaLeaderboard(guild, context.Bot, timestamp);
                        break;
                    case LeaderboardType.Custom:
                        leaderboard = await Leaderboard.GenerateTimeBasedLeaderboard(guild, context.Bot, type, timestamp, customHours);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
                return new SuccessResult(await leaderboard.ToStringAsync());
            }
        }

        [Command("analytics"), HelpText("Generates a JSON file with analytics from the server"), CommandScope(ChannelType.Text), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> Analytics(DiscordUserMessageContext context)
        {
            await context.Reply("This is going to take a while");
            // Channel Name => User Name => Date => Hour
            List<string> data = new List<string>();
            data.Add("Channel\tUser\tIsBot\tTimestamp\tEditedTimestamp\tMessageLength\tHasRichEmbed\tHasAttachment\tReactions");
            var channels = await context.Guild.GetTextChannelsAsync();

            foreach (ITextChannel channel in channels)
            {
                ChannelPermissions permissions = (await context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (!permissions.ReadMessages || !permissions.ReadMessageHistory)
                {
                    continue;
                }

                int messagesInChannel = 0;

                var pages = channel.GetMessagesAsync(int.MaxValue);
                pages.ForEach(
                page =>
                {
                    foreach (IMessage message in page)
                    {
                        var timestampPacific = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(message.Timestamp, "Pacific Standard Time");
                        DateTimeOffset? editedTimestampPacific = null;
                        if (message.EditedTimestamp != null)
                            editedTimestampPacific = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(message.EditedTimestamp.Value, "Pacific Standard Time");
                        data.Add($"{message.Channel.Name}\t{message.Author}\t{message.Author.IsBot}\t{timestampPacific.DateTime:G}\t{editedTimestampPacific?.ToString("G") ?? ""}\t{message.Content.Length}\t{message.Embeds.Any(x => x.Type == EmbedType.Rich)}\t{message.Attachments.Count > 0}\t{((IUserMessage)message).Reactions.Count}");
                    }
                });
            }

            File.WriteAllLines(SettingsManager.BasePath + $"analytics-{context.Guild.Id}.txt", data);
            await context.Channel.SendFileAsync(SettingsManager.BasePath + $"analytics-{context.Guild.Id}.txt");
            return new SuccessResult();
        }
    }
}
