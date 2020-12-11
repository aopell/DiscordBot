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
        [Permissions(guildPermissions: new[] { GuildPermission.ReadMessageHistory, GuildPermission.ManageMessages, GuildPermission.ViewChannel }), ChannelDescriptionDelay(21600)]
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

            IUserMessage leaderboardMessage = null;

            if (context is DiscordUserMessageContext)
            {
                leaderboardMessage = await messageChannel.SendMessageAsync("Calculating messages sent. This may take a while...");
                await leaderboardMessage.AddReactionAsync(CommandTools.LoadingEmote);
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

                if (context is DiscordUserMessageContext umc)
                {
                    bool first = true;
                    foreach (string message in await leaderboard.ToStringsAsync())
                    {
                        if (first)
                        {
                            await umc.Message.ReplyAsync(message);
                            first = false;
                            continue;
                        }
                        await context.Reply(message);
                    }

                    if (leaderboardMessage != null)
                        await leaderboardMessage.DeleteAsync();

                    return new SuccessResult();
                }
                else
                {
                    return new SuccessResult(await leaderboard.ToStringAsync());
                }
            }
        }

        [Command("analytics"), HelpText("Generates a tab separated text file with analytics from the server"), CommandScope(ChannelType.Text), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> Analytics(DiscordUserMessageContext context, bool includeMessageText = false)
        {
            await (await context.Channel.SendMessageAsync("This is going to take a while")).AddReactionAsync(CommandTools.LoadingEmote);

            using (context.Channel.EnterTypingState())
            {
                // Channel Name => User Name => Date => Hour
                List<string> data = new List<string>();
                data.Add($"MessageID\tChannel\tUser\tIsBot\tTimestamp\tUnixTimestamp\tEditedTimestamp\tUnixEditedTimestamp\tMessageLength\tEmbedType\tHasAttachment\tReactionCount{(includeMessageText ? "\tMessage Text" : "")}");
                var channels = await context.Guild.GetTextChannelsAsync();

                foreach (ITextChannel channel in channels)
                {
                    ChannelPermissions permissions = (await context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                    if (!permissions.ViewChannel || !permissions.ReadMessageHistory)
                    {
                        continue;
                    }

                    var pages = channel.GetMessagesAsync(int.MaxValue);
                    await pages.ForEachAsync(
                    page =>
                    {
                        foreach (IMessage message in page)
                        {
                            var timestampLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(message.Timestamp, context.Bot.DefaultTimeZone);
                            DateTimeOffset? editedTimestampLocal = null;
                            if (message.EditedTimestamp != null)
                                editedTimestampLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(message.EditedTimestamp.Value, context.Bot.DefaultTimeZone);
                            data.Add($"{message.Id}\t{message.Channel.Name}\t{message.Author}\t{message.Author.IsBot}\t{timestampLocal.DateTime:G}\t{timestampLocal.ToUnixTimeSeconds()}\t{editedTimestampLocal?.ToString("G") ?? ""}\t{editedTimestampLocal?.ToUnixTimeSeconds().ToString() ?? ""}\t{new System.Globalization.StringInfo(message.Content).LengthInTextElements}\t{message.Embeds.FirstOrDefault()?.Type.ToString() ?? ""}\t{message.Attachments.Count > 0}\t{(message as IUserMessage)?.Reactions.Count ?? 0}{(includeMessageText ? $"\t{message.Content.Replace("\n", "␊").Replace("\r", "")}" : "")}");
                        }
                    });
                }

                File.WriteAllLines(Config.BasePath + $"analytics-{context.Guild.Id}.txt", data);

                if (!includeMessageText)
                {
                    using (var stream = File.OpenRead(Config.BasePath + $"analytics-{context.Guild.Id}.txt"))
                    {
                        await context.Channel.SendFileAsync(stream, $"analytics-{context.Guild.Id}-{DateTimeOffset.Now.ToUnixTimeSeconds()}.txt");
                    }
                }
                else
                {
                    await context.Message.ReplyAsync($"Finished creating analytics file. Saved as `analytics-{context.Guild.Id}.txt` ({Math.Round(new FileInfo(Config.BasePath + $"analytics-{context.Guild.Id}.txt").Length / 1048576d, 2)} MB)");
                }
            }
            return new SuccessResult();
        }
    }
}
