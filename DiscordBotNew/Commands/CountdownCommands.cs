using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;

namespace DiscordBotNew.Commands
{
    public static class CountdownCommands
    {
        [Command("countdowns"), HelpText("Lists countdowns for the current server"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> CountdownList(ICommandContext context, int page = 1)
        {
            const int pageSize = 10;

            IGuildChannel channel;
            switch (context)
            {
                case DiscordMessageContext message:
                    channel = (IGuildChannel)message.Channel;
                    break;
                case DiscordChannelDescriptionContext desc:
                    channel = desc.Channel;
                    break;
                default:
                    return new ErrorResult($"The `countdowns` command is not valid in the context `{context.GetType().Name}`");
            }

            var countdowns = context.Bot.Countdowns.Countdowns.GetValueOrDefault(channel.GuildId) ?? new Dictionary<string, DateTimeOffset>();

            if (page <= 0 || page > countdowns.Count / pageSize + 1)
            {
                return new ErrorResult($"Please use a valid page number (1-{countdowns.Count / pageSize + 1})");
            }

            EmbedBuilder countdownEmbed = new EmbedBuilder()
                                         .WithTitle("Countdowns")
                                         .WithColor(0x7689d8)
                                         .WithThumbnailUrl("https://emojipedia-us.s3.amazonaws.com/thumbs/120/twitter/120/stopwatch_23f1.png");

            if (context is DiscordMessageContext m)
            {
                await PaginatedCommand.SendPaginatedMessage(m, "countdowns", countdowns.OrderBy(x => x.Value).Select(countdown => new KeyValuePair<string, string>(countdown.Key, (countdown.Value - DateTimeOffset.Now).ToLongString())).ToList(), page, pageSize, countdownEmbed);
                return new SuccessResult();
            }

            foreach (var countdown in countdowns.OrderBy(x => x.Value).Skip((page - 1) * pageSize).Take(pageSize))
            {
                countdownEmbed.AddField(countdown.Key, (countdown.Value - DateTimeOffset.Now).ToLongString());
            }

            return new SuccessResult(embed: countdownEmbed.Build());
        }

        [Command("countdown"), HelpText("Creates, edits, or deletes a countdown"), CommandScope(ChannelType.Text)]
        public static ICommandResult Countdown(ICommandContext context, CountdownAction action, string name, [JoinRemainingParameters, DisplayName("event date/time"), HelpText("ex. \"1/1/11 1:11 PM\"")] DateTimeOffset? date = null)
        {
            IGuildChannel channel;
            switch (context)
            {
                case DiscordMessageContext message:
                    channel = (IGuildChannel)message.Channel;
                    break;
                case DiscordChannelDescriptionContext desc:
                    channel = desc.Channel;
                    break;
                default:
                    return new ErrorResult($"The `countdown` command is not valid in the context `{context.GetType().Name}`");
            }

            context.Bot.Countdowns.Countdowns = context.Bot.Countdowns.Countdowns ?? new Dictionary<ulong, Dictionary<string, DateTimeOffset>>();
            var countdowns = context.Bot.Countdowns.Countdowns.GetValueOrDefault(channel.GuildId) ?? new Dictionary<string, DateTimeOffset>();
            switch (action)
            {
                case CountdownAction.Create:
                case CountdownAction.Add:
                    if (countdowns.Select(x => x.Key.ToLower()).Contains(name.ToLower()))
                    {
                        return new ErrorResult($"The countdown with the name {name} already exists");
                    }
                    break;
                case CountdownAction.Edit:
                    if (!countdowns.Select(x => x.Key.ToLower()).Contains(name.ToLower()))
                    {
                        return new ErrorResult($"The countdown with the name {name} does not exist");
                    }
                    break;
                case CountdownAction.Delete:
                    if (!countdowns.Select(x => x.Key.ToLower()).Contains(name.ToLower())) return new ErrorResult($"The countdown with the name {name} does not exist");
                    countdowns = countdowns.Where(x => x.Key.ToLower() != name.ToLower()).ToDictionary(x => x.Key, x => x.Value);
                    context.Bot.Countdowns.Countdowns[channel.GuildId] = countdowns;
                    context.Bot.Countdowns.SaveConfig();
                    return new SuccessResult($"Successfully deleted countdown {name}");
            }

            if (!date.HasValue) return new ErrorResult("Please provide a date and time when creating or editing a countdown");
            var existingCountdown = countdowns.Select(x => x.Key).FirstOrDefault(x => x.ToLower() == name.ToLower());
            if (existingCountdown != null)
            {
                countdowns[existingCountdown] = date.Value;
            }
            else
            {
                countdowns.Add(name, date.Value);
            }
            context.Bot.Countdowns.Countdowns[channel.GuildId] = countdowns;
            context.Bot.Countdowns.SaveConfig();

            return GenerateCountdownResult(context, name, countdowns[name]);
        }

        private static SuccessResult GenerateCountdownResult(ICommandContext context, string name, DateTimeOffset date) => context is DiscordMessageContext ? new SuccessResult(embed: CommandTools.GenerateCountdownEmbed(context.Bot, name, date).Build()) : new SuccessResult($"{(date.ToUniversalTime() - DateTimeOffset.Now).ToLongString()} until {name}");

        [Command("countdown"), HelpText("Views the status of a countdown timer"), CommandScope(ChannelType.Text)]
        public static ICommandResult Countdown(ICommandContext context, [JoinRemainingParameters] string name)
        {
            IGuildChannel channel;
            switch (context)
            {
                case DiscordMessageContext message:
                    channel = (IGuildChannel)message.Channel;
                    break;
                case DiscordChannelDescriptionContext desc:
                    channel = desc.Channel;
                    break;
                default:
                    return new ErrorResult($"The `countdown` command is not valid in the context `{context.GetType().Name}`");
            }

            var countdowns = context.Bot.Countdowns.Countdowns.GetValueOrDefault(channel.GuildId) ?? new Dictionary<string, DateTimeOffset>();

            if (countdowns.All(x => x.Key.ToLower() != name.ToLower()))
            {
                return new ErrorResult($"No countdown with the name {name} was found. Try creating it.");
            }

            return GenerateCountdownResult(context, name, countdowns.First(x => x.Key.ToLower() == name.ToLower()).Value);
        }

        [Command("nextcountdown"), HelpText("Views the status of the next upcoming countdown timer"), CommandScope(ChannelType.Text)]
        public static ICommandResult NextCountdown(ICommandContext context, int countdownNumber = 1)
        {
            IGuildChannel channel;
            switch (context)
            {
                case DiscordMessageContext message:
                    channel = (IGuildChannel)message.Channel;
                    break;
                case DiscordChannelDescriptionContext desc:
                    channel = desc.Channel;
                    break;
                default:
                    return new ErrorResult($"The `countdown` command is not valid in the context `{context.GetType().Name}`");
            }

            var countdowns = (context.Bot.Countdowns.Countdowns.GetValueOrDefault(channel.GuildId) ?? new Dictionary<string, DateTimeOffset>()).Where(x => x.Value > DateTimeOffset.Now).OrderBy(x => x.Value).ToArray();
            if (!countdowns.Any()) return new ErrorResult("No countdowns");
            if (countdownNumber > 0 && countdowns.Length >= countdownNumber)
            {
                var next = countdowns[countdownNumber - 1];
                return GenerateCountdownResult(context, next.Key, next.Value);
            }

            return new ErrorResult("Invalid countdown number");
        }

        [Command("countdownannouncements"), HelpText("Manages countdown complete announcements for the server"), CommandScope(ChannelType.Text), Permissions(guildPermissions: new[] { GuildPermission.ManageChannels })]
        public static ICommandResult CountdownSettings(DiscordUserMessageContext context, AnnounceCountdownAction action)
        {
            var channels = context.Bot.Countdowns.CountdownChannels ?? new Dictionary<ulong, ulong>();
            switch (action)
            {
                case AnnounceCountdownAction.Set:
                    if (context.Message.MentionedChannelIds.Count == 0) return new ErrorResult("Please mention a channel to be used for countdown announcements.");
                    channels[context.Guild.Id] = context.Message.MentionedChannelIds.First();
                    context.Bot.Countdowns.CountdownChannels = channels;
                    context.Bot.Countdowns.SaveConfig();
                    return new SuccessResult($"Successfully enabled countdown announcements in <#{context.Message.MentionedChannelIds.First()}>");
                case AnnounceCountdownAction.Unset:
                    if (!channels.ContainsKey(context.Guild.Id))
                    {
                        return new ErrorResult("This server does not have an announcements channel specified");
                    }
                    channels.Remove(context.Guild.Id);
                    context.Bot.Countdowns.CountdownChannels = channels;
                    context.Bot.Countdowns.SaveConfig();
                    return new SuccessResult("Successfully disabled countdown announcements");
            }

            return new ErrorResult("Unknown action");
        }

        public enum CountdownAction
        {
            [HelpText("Creates a new countdown")] Create,
            [HelpText("Alias for create")] Add,
            [HelpText("Changes the time of an existing countdown")] Edit,
            [HelpText("Deletes an existing countdown")] Delete
        }

        public enum AnnounceCountdownAction
        {
            [HelpText("Enables countdown announcements in the mentioned channel")] Set = 0,
            [HelpText("Disables countdown announcements for the server")] Unset = 1,
            [HelpText("Alias for `Unset`")] Remove = 1
        }
    }
}
