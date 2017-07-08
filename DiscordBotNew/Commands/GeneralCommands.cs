using System;
using System.Collections.Generic;
using System.IO;
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
        public static ICommandResult Echo(ICommandContext context, [JoinRemainingParameters, HelpText("The message to repeat")] string text) => new SuccessResult(text);

        [Command("8ball"), HelpText("It knows your future")]
        public static ICommandResult Magic8Ball(ICommandContext context, [JoinRemainingParameters, HelpText("A yes or no question")] string question)
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
        public static ICommandResult SetPrefix(DiscordMessageContext context, [JoinRemainingParameters, HelpText("The new prefix for this channel or server (up to 16 characters)")] string prefix)
        {
            if (prefix.Length > 16)
            {
                return new ErrorResult("Prefix must be no more than 16 characters in length");
            }

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

            if (context.Bot.Settings.GetSetting("customPrefixes", out Dictionary<ulong, string> prefixes))
            {
                if (prefixes.ContainsKey(id))
                {
                    prefixes[id] = prefix;
                }
                else
                {
                    prefixes.Add(id, prefix);
                }

                context.Bot.Settings.AddSetting("customPrefixes", prefixes);
            }
            else
            {
                context.Bot.Settings.AddSetting("customPrefixes", new Dictionary<ulong, string>
                {
                    { id, prefix }
                });
            }

            context.Bot.Settings.SaveSettings();

            return new SuccessResult($"Prefix set to `{prefix}` for this {(server ? "server" : "channel")}");
        }

        [Command("quote", "byid"), HelpText("Quotes the message with the provided ID number"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> Quote(DiscordMessageContext context, [DisplayName("message ID"), HelpText("The message to quote")] ulong id, [DisplayName("channel mention"), HelpText("The channel to search")] string channel = null)
        {
            IMessage msg;
            if (context.Message.MentionedChannels.Count != 0)
            {
                msg = await ((ISocketMessageChannel)context.Bot.Client.GetChannel(context.Message.MentionedChannels.First().Id)).GetMessageAsync(id);
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
                    IconUrl = msg.Author.AvatarUrlOrDefaultAvatar()
                },
                Timestamp = msg.Timestamp,
                Description = msg.Content
            };

            if (msg.Attachments.Count > 0)
                builder.ImageUrl = msg.Attachments.First().Url;

            foreach (Embed embed in msg.Embeds.Where(x => x.Type == EmbedType.Rich))
            {
                await msg.Channel.SendMessageAsync("", embed: embed);
            }

            return new SuccessResult("", embed: builder);
        }

        [Command("countdown"), HelpText("Creates or views the status of a countdown timer")]
        public static ICommandResult Countdown(ICommandContext context, string name, [JoinRemainingParameters, HelpText("The time to count down to")] DateTime? date = null, [DisplayName("Windows TimeZone ID")] string timezone = "Pacific Standard Time")
        {
            var countdowns = context.Bot.Settings.GetSetting("countdowns", out Dictionary<string, DateTimeOffset> cd) ? cd : new Dictionary<string, DateTimeOffset>();

            if (date != null)
            {
                var pacificTime = new DateTimeOffset(date.Value, TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, timezone).Offset);

                if (countdowns.ContainsKey(name))
                {
                    countdowns[name] = pacificTime;
                }
                else
                {
                    countdowns.Add(name, pacificTime);
                }

                context.Bot.Settings.AddSetting("countdowns", countdowns);
                context.Bot.Settings.SaveSettings();
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

        [Command("back"), HelpText("Creates a backronym from the given text")]
        public static ICommandResult Back(ICommandContext context, string acronym, [HelpText("The number of backronyms to generate (up to 10)")]byte count = 1, [HelpText("Whether or not to use the larger English dictionary")] bool useComplexWords = false)
        {
            count = count < 1 ? (byte)1 : count > 10 ? (byte)10 : count;

            string backronym = "";
            for (int i = 0; i < count; i++)
            {
                backronym += acronym.ToUpper() + ": ";
                foreach (char c in acronym)
                {
                    if (File.Exists($"{(useComplexWords ? "" : "simple")}words\\{char.ToLower(c)}.txt"))
                    {
                        string[] words = File.ReadAllLines($"{(useComplexWords ? "" : "simple")}words\\{char.ToLower(c)}.txt");
                        string word = words[random.Next(words.Length)];
                        if (word.Length > 1)
                            backronym += char.ToUpper(word[0]) + word.Substring(1) + " ";
                        else
                            backronym += char.ToUpper(word[0]) + " ";
                    }
                }

                backronym += "\n";
            }

            return new SuccessResult(backronym);
        }

        [Command("leaderboard"), HelpText("Counts messages sent by each person in a server"), CommandScope(ChannelType.Text)]
        public async static Task<ICommandResult> Leaderboard(ICommandContext context)
        {
            IGuild guild;
            ITextChannel messageChannel;
            switch (context)
            {
                case DiscordMessageContext d:
                    guild = d.Guild;
                    messageChannel = (ITextChannel)d.Channel;
                    break;
                case DiscordChannelDescriptionContext d:
                    guild = d.Channel.Guild;
                    messageChannel = (ITextChannel)d.Channel;
                    break;
                default:
                    return new ErrorResult($"The `leaderboard` command is not valid in the context `{context.GetType().Name}`");
            }

            await messageChannel.SendMessageAsync("Calculating messages sent. This may take a few minutes...");

            using (messageChannel.EnterTypingState())
            {

                var channels = await guild.GetTextChannelsAsync();
                Dictionary<ulong, int> messagesPerUser = new Dictionary<ulong, int>();
                Dictionary<ulong, int> messagesPerChannel = new Dictionary<ulong, int>();
                Dictionary<ulong, string> usernameLookup = new Dictionary<ulong, string>();
                int totalMessages = 0;

                foreach (var channel in channels)
                {
                    IEnumerable<IMessage> messages = await channel.GetMessagesAsync().Flatten();
                    IMessage lastMessage = null;
                    while (lastMessage != messages.Last())
                    {
                        lastMessage = messages.Last();
                        messages = messages.Concat(await channel.GetMessagesAsync(lastMessage, Direction.Before).Flatten());
                    }

                    int messagesInChannel = 0;
                    foreach (var message in messages)
                    {
                        if (!messagesPerUser.ContainsKey(message.Author.Id))
                        {
                            messagesPerUser.Add(message.Author.Id, 0);
                            usernameLookup.Add(message.Author.Id, message.Author.NicknameOrUsername());
                        }

                        messagesPerUser[message.Author.Id]++;
                        messagesInChannel++;
                    }
                    messagesPerChannel[channel.Id] = messagesInChannel;
                    totalMessages += messagesInChannel;
                }

                var userMessages = messagesPerUser.ToList().OrderByDescending(x => x.Value);
                var channelMessages = messagesPerChannel.ToList().OrderByDescending(x => x.Value);

                StringBuilder builder = new StringBuilder("**Messages Leaderboard**\n```\n");
                builder.AppendLine("Channels");
                foreach (var channel in channelMessages)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   #{2}\n", channel.Value, channel.Value / (float)totalMessages * 100, (await guild.GetChannelAsync(channel.Key)).Name);
                }
                builder.AppendLine("\nUsers");
                foreach (var user in userMessages)
                {
                    builder.AppendFormat("{0,-7}({1,4:0.0}%)   {2}\n", user.Value, user.Value / (float)totalMessages * 100, usernameLookup[user.Key]);
                }
                builder.AppendLine($"\nTotal messages in server: {totalMessages}");
                builder.Append("```");

                return new SuccessResult(builder.ToString());
            }
        }
    }
}
