using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew.Commands
{
    public static class GeneralCommands
    {
        private static Random random = new Random();

        [Command("hello", "test"), HelpText("Says hi")]
        public static ICommandResult Hello(ICommandContext context) => new SuccessResult("Hello there! :hand_splayed:");

        [Command("echo", "say"), HelpText("Repeats the provided text back to you")]
        public static ICommandResult Echo(ICommandContext context, [JoinRemainingParameters] string text) => new SuccessResult(text);

        [Command("8ball"), HelpText("It knows your future")]
        public static ICommandResult Magic8Ball(ICommandContext context, [HelpText("yes or no question"), JoinRemainingParameters] string question)
        {
            string[] responses = {
                "It is certain",
                "It is decidedly so",
                "Without a doubt",
                "Yes, definitely",
                "You may rely on it",
                "As I see it, yes",
                "Most likely",
                "Outlook good",
                "Yes",
                "Signs point to yes",
                "Reply hazy try again",
                "Ask again later",
                "Better not tell you now",
                "Cannot predict now",
                "Concentrate and ask again",
                "Don't count on it",
                "My reply is no",
                "My sources say no",
                "Outlook not so good",
                "Very doubtful"
            };

            var messageContext = context as DiscordMessageContext;
            if (messageContext != null)
                return new SuccessResult($"<@{messageContext.MessageAuthor.Id}>: ***{question}***\n" + responses[random.Next(responses.Length)]);
            return new SuccessResult(responses[random.Next(responses.Length)]);
        }

        [Command("setprefix"), HelpText("Sets the command prefix for this DM channel or server"), Permissions(guildPermissions: new[] { GuildPermission.ManageGuild })]
        public static ICommandResult SetPrefix(DiscordMessageContext context, [JoinRemainingParameters] string prefix)
        {
            bool server = false;
            ulong id;
            if (context.ChannelType == ChannelType.Text)
            {
                id = context.Guild?.Id ?? context.Channel.Id;
                server = context.Guild != null;
            }
            else
            {
                id = context.Channel.Id;
            }

            if (SettingsManager.GetSetting("customPrefixes", out Dictionary<ulong, string> prefixes))
            {
                if (prefixes.ContainsKey(id))
                {
                    prefixes[id] = prefix;
                }
                else
                {
                    prefixes.Add(id, prefix);
                }

                SettingsManager.AddSetting("customPrefixes", prefixes);
            }
            else
            {
                SettingsManager.AddSetting("customPrefixes", new Dictionary<ulong, string>
                {
                    { id, prefix }
                });
            }

            SettingsManager.SaveSettings();

            return new SuccessResult($"Prefix set to `{prefix}` for this {(server ? "server" : "channel")}");
        }

        [Command("quote", "byid"), HelpText("Quotes the message with the provided ID number"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> Quote(DiscordMessageContext context, ulong id, [HelpText("channel mention")] string channel = null)
        {
            IMessage msg;
            if (context.Message.MentionedChannels.Count != 0)
            {
                msg = await ((ISocketMessageChannel)context.BotClient.GetChannel(context.Message.MentionedChannels.First().Id)).GetMessageAsync(id);
            }
            else
            {
                msg = await context.Channel.GetMessageAsync(id);
            }

            if (msg == null) return new ErrorResult("Message not found");

            var builder = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = msg.Author.NicknameOrUsername(),
                    IconUrl = msg.Author.GetAvatarUrl() ?? "https://discordapp.com/assets/6debd47ed13483642cf09e832ed0bc1b.png"
                },
                Timestamp = msg.Timestamp,
                Description = msg.Content
            };

            if (msg.Attachments.Count > 0)
                builder.ImageUrl = msg.Attachments.First().Url;

            return new SuccessResult("", embed: builder);
        }

        [Command("countdown"), HelpText("Creates or views the status of a countdown timer")]
        public static ICommandResult Countdown(ICommandContext context, string name, [JoinRemainingParameters] DateTime? date = null)
        {
            var countdowns = SettingsManager.GetSetting("countdowns", out Dictionary<string, DateTimeOffset> cd) ? cd : new Dictionary<string, DateTimeOffset>();

            if (date != null)
            {
                var pacificTime = new DateTimeOffset(date.Value, TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time").Offset);

                if (countdowns.ContainsKey(name))
                {
                    countdowns[name] = pacificTime;
                }
                else
                {
                    countdowns.Add(name, pacificTime);
                }

                SettingsManager.AddSetting("countdowns", countdowns);
                SettingsManager.SaveSettings();
            }

            if (!countdowns.ContainsKey(name))
            {
                return new ErrorResult($"No countdown with the name {name} was found. Try creating it.");
            }

            TimeSpan difference = countdowns[name] - DateTimeOffset.Now;

            var response = new StringBuilder();
            response.Append(difference.Days != 0 ? $"{difference.Days} days " : "");
            response.Append($"{difference.Hours} hours ");
            response.Append($"{difference.Minutes} minutes ");
            response.Append($"{difference.Seconds} seconds ");
            response.Append($"until {name}");

            return new SuccessResult(response.ToString());
        }
    }
}
