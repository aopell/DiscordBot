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
        public static async Task Hello(SocketMessage message)
        {
            await message.Reply("Hello there! :hand_splayed:");
        }

        [Command("echo", "say"), HelpText("Repeats the provided text back to you")]
        public static async Task Echo(SocketMessage message, [JoinRemainingParameters] string text)
        {
            await message.Reply(text);
        }

        [Command("8ball"), HelpText("It knows your future")]
        public static async Task Magic8Ball(SocketMessage message, [HelpText("yes or no question"), JoinRemainingParameters] string question)
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

            await message.Reply($"<@{message.Author.Id}>: ***{question}***\n" + responses[random.Next(responses.Length)]);
        }

        [Command("setprefix"), HelpText("Sets the command prefix for this DM channel or server"), Permissions(guildPermissions: new[] { GuildPermission.ManageGuild })]
        public static async Task SetPrefix(SocketMessage message, [JoinRemainingParameters] string prefix)
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

            await message.Reply($"Prefix set to `{prefix}` for this {(server ? "server" : "channel")}");
        }

        [Command("quote", "byid"), HelpText("Quotes the message with the provided ID number"), CommandScope(ChannelType.Text)]
        public static async Task Quote(SocketMessage message, ulong id, [HelpText("channel mention")] string channel = null)
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

            StringBuilder reply = new StringBuilder();
            reply.Append($"Message sent by {msg.Author.Username}#{msg.Author.Discriminator} at {TimeZoneInfo.ConvertTimeBySystemTimeZoneId(msg.Timestamp, "Pacific Standard Time")} PT:\n");

            if (!string.IsNullOrWhiteSpace(msg.Content))
                reply.Append($"{msg.Content}\n");

            if (msg.Attachments.Count > 0)
                reply.Append($"Attachments:\n{string.Join("\n", msg.Attachments.Select(att => att.Url))}");

            await message.Reply(reply.ToString());
            //foreach (IAttachment attachment in msg.Attachments)
            //{
            //    await message.Channel.SendFileAsync(await new HttpClient().GetStreamAsync(attachment.Url), attachment.Filename);
            //}

            foreach (Embed embed in msg.Embeds.OfType<Embed>())
            {
                await message.Reply("", embed: embed);
            }
        }
    }
}
