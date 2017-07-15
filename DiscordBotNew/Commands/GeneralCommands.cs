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
                await context.Channel.SendMessageAsync("", embed: embed);
            }

            return new SuccessResult(embed: builder);
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

            return new SuccessResult(difference.ToLongString());
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
        public static async Task<ICommandResult> GenerateLeaderboard(ICommandContext context, LeaderboardType type = LeaderboardType.Delta)
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

            await messageChannel.SendMessageAsync("Calculating messages sent. This may take a few seconds...");

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
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
                return new SuccessResult(await leaderboard.ToStringAsync());
            }
        }

        [Command("status"), HelpText("Gets when a user was last online")]
        public static async Task<ICommandResult> Status(ICommandContext context, [DisplayName("username | @ mention")] string user)
        {
            SocketUser targetUser;

            switch (context)
            {
                case DiscordMessageContext discordContext:
                    if (discordContext.Message.MentionedUsers.Count > 0)
                    {
                        targetUser = discordContext.Message.MentionedUsers.First();
                    }
                    else if (discordContext.Message.Tags.Count > 0 && discordContext.Message.Tags.First().Type == TagType.UserMention && (targetUser = context.Bot.Client.GetUser(discordContext.Message.Tags.First().Key)) != null)
                    {
                    }
                    else
                    {
                        targetUser = (SocketUser)await CommandTools.GetUserByUsername(user, discordContext.Channel);
                    }
                    break;
                case DiscordChannelDescriptionContext channelDescription:
                    targetUser = (SocketUser)await CommandTools.GetUserByUsername(user, (ISocketMessageChannel)channelDescription.Channel);
                    break;
                default:
                    return new ErrorResult($"The `status` command is not valid in the context `{context.GetType().Name}`");
            }

            if (targetUser == null || !context.Bot.UserStatuses.GetSetting("statuses", out Dictionary<ulong, UserStatusInfo> statuses))
            {
                return new ErrorResult("No status history data found");
            }

            if (statuses.ContainsKey(targetUser.Id))
            {
                var statusInfo = statuses[targetUser.Id];

                Color? color;
                switch (targetUser.Status)
                {
                    case UserStatus.Online:
                        color = new Color(76, 175, 80);
                        break;
                    case UserStatus.Idle:
                    case UserStatus.AFK:
                        color = new Color(255, 235, 59);
                        break;
                    case UserStatus.DoNotDisturb:
                        color = new Color(244, 67, 54);
                        break;
                    default:
                        color = null;
                        break;
                }

                EmbedBuilder statusEmbed = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        Name = targetUser.NicknameOrUsername(),
                        IconUrl = targetUser.AvatarUrlOrDefaultAvatar()
                    },
                    Color = color,
                    Description = targetUser.Status.ToString()
                };

                statusEmbed.AddInlineField($"{targetUser.Status} For", (DateTimeOffset.Now - statusInfo.StatusLastChanged).ToLongString());
                if (targetUser.Status != UserStatus.Online)
                    statusEmbed.AddInlineField("Last Online", $"{(DateTimeOffset.Now - statusInfo.LastOnline).ToLongString()} ago");

                return new SuccessResult(embed: statusEmbed);
            }

            return new ErrorResult("User status history not found");
        }

        [Command("cat"), HelpText("Cat.")]
        public static async Task<ICommandResult> Cat(DiscordMessageContext context)
        {
            using (context.Channel.EnterTypingState())
            {
                string url = "http://thecatapi.com/api/images/get?format=src&type=png";
                HttpClient client = new HttpClient();
                await context.Channel.SendFileAsync(await client.GetStreamAsync(url), "cat.png");
            }
            return new SuccessResult();
        }
    }
}
