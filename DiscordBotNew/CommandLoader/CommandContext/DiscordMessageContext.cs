using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public abstract class DiscordMessageContext : ICommandContext
    {
        public IUserMessage Message { get; }
        public ChannelType ChannelType => Message.GetChannelType();
        public IMessageChannel Channel => Message.Channel;
        public IUser MessageAuthor => Message.Author;
        public IGuild Guild => (Channel as IGuildChannel)?.Guild;

        public DiscordBot Bot { get; }

        public DiscordMessageContext(IUserMessage message, DiscordBot bot)
        {
            Message = message;
            Bot = bot;
        }
        public virtual async Task Reply(string message) => await Reply(message, false);
        public virtual async Task Reply(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "Reply", $"{Guild?.Name ?? "DM"} #{Channel.Name}: {message}"));
            await Channel.SendMessageAsync(message, isTTS, embed, options);
        }

        public virtual async Task ReplyError(Exception ex)
        {
#if DEBUG
            string message = ex.ToString();
            const int maxMessageLength = 1500;
            foreach (string m in Enumerable.Range(0, message.Length / maxMessageLength + 1).Select(i => message.Substring(i * maxMessageLength, message.Length - i * maxMessageLength > maxMessageLength ? maxMessageLength : message.Length - i * maxMessageLength)))
            {
                await ReplyError(m, ex.GetType().Name);
            }
#else
            await ReplyError(ex.Message, ex.GetType().Name);
#endif
        }
        public virtual async Task ReplyError(string description, string title = "Error")
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Error, "ErrorReply", $"{(Channel as IGuildChannel)?.Guild.Name ?? "DM"} #{Channel.Name}: [{title}] {description}"));
            await Channel.SendMessageAsync(string.Empty, embed: BuildErrorEmbed(description, title));
        }
        public virtual LogMessage LogMessage(string commandName) => new LogMessage(LogSeverity.Info, "Command", $"@{MessageAuthor.Username}#{MessageAuthor.Discriminator} in {(Channel as IGuildChannel)?.Guild.Name ?? "DM"} #{Channel.Name}: [{commandName}] {Message.Content}");

        protected static Embed BuildErrorEmbed(string description, string title = "Error")
        {
            var embed = new EmbedBuilder
            {
                Title = title,
                Description = description,
                Color = new Color(244, 67, 54),
                ThumbnailUrl = "https://images-ext-2.discordapp.net/external/vdhk1oNVxVSy7fONtZIGDb6GjZdWO2mbrmcmBTgnsd0/https/images.discordapp.net/attachments/239605336481333248/336350347956191232/errorcat.png",
                Footer = new EmbedFooterBuilder
                {
                    Text = "If you believe this should not have happened, please submit a bug report"
                }
            };

            return embed;
        }

        protected static Embed BuildErrorEmbed(Exception error) => BuildErrorEmbed(error.Message, $"Error - {error.GetType().Name}");
    }
}
