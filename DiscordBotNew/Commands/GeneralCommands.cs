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
        public static ICommandResult Hello(SocketMessage message) => new SuccessResult("Hello there! :hand_splayed:");

        [Command("echo", "say"), HelpText("Repeats the provided text back to you")]
        public static ICommandResult Echo(SocketMessage message, [JoinRemainingParameters] string text) => new SuccessResult(text);

        [Command("8ball"), HelpText("It knows your future")]
        public static ICommandResult Magic8Ball(SocketMessage message, [HelpText("yes or no question"), JoinRemainingParameters] string question)
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

            return new SuccessResult($"<@{message.Author.Id}>: ***{question}***\n" + responses[random.Next(responses.Length)]);
        }

        [Command("setprefix"), HelpText("Sets the command prefix for this DM channel or server"), Permissions(guildPermissions: new[] { GuildPermission.ManageGuild })]
        public static ICommandResult SetPrefix(SocketMessage message, [JoinRemainingParameters] string prefix)
        {
            bool server = false;
            ulong id;
            if (message.GetChannelType() == ChannelType.Text)
            {
                var guildChannel = message.Channel as IGuildChannel;
                id = guildChannel?.Guild.Id ?? message.Channel.Id;
                server = guildChannel != null;
            }
            else
            {
                id = message.Channel.Id;
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
        public static async Task<ICommandResult> Quote(SocketMessage message, ulong id, [HelpText("channel mention")] string channel = null)
        {
            IMessage msg;
            if (message.MentionedChannels.Count != 0)
            {
                msg = await ((ISocketMessageChannel)DiscordBot.Client.GetChannel(message.MentionedChannels.First().Id)).GetMessageAsync(id);
            }
            else
            {
                msg = await message.Channel.GetMessageAsync(id);
            }

            if (msg == null) return new ErrorResult("Message not found");

            var reply = new StringBuilder();
            reply.Append($"Message sent by {msg.Author.Username}#{msg.Author.Discriminator} at {TimeZoneInfo.ConvertTimeBySystemTimeZoneId(msg.Timestamp, "Pacific Standard Time")} PT:\n");

            if (!string.IsNullOrWhiteSpace(msg.Content))
                reply.Append($"{msg.Content}\n");

            if (msg.Attachments.Count > 0)
                reply.Append($"Attachments:\n{string.Join("\n", msg.Attachments.Select(att => att.Url))}");

            await message.Reply(reply.ToString());

            foreach (Embed embed in msg.Embeds.OfType<Embed>())
            {
                await message.Reply("", embed: embed);
            }

            return new SuccessResult();
        }

        [Command("countdown"), HelpText("Creates or views the status of a countdown timer")]
        public static ICommandResult Countdown(SocketMessage message, string name, [JoinRemainingParameters] DateTime? date = null)
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
