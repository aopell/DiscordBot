using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public class DiscordMessageContext : ICommandContext
    {
        public SocketMessage Message { get; }
        public ChannelType ChannelType => Message.GetChannelType();
        public ISocketMessageChannel Channel => Message.Channel;
        public SocketUser MessageAuthor => Message.Author;
        public IGuild Guild => (Channel as IGuildChannel)?.Guild;

        public DiscordBot Bot { get; }

        public DiscordMessageContext(SocketMessage message, DiscordBot bot)
        {
            Message = message;
            Bot = bot;
        }
        public async Task Reply(string message) => await Reply(message, false);
        public async Task Reply(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "Reply", $"{Guild?.Name ?? "DM"} #{Channel.Name}: {message}"));
            await Channel.SendMessageAsync(message, isTTS, embed, options);
        }
        public async Task ReplyError(Exception ex) => await ReplyError(ex.Message, ex.GetType().Name);

        public async Task ReplyError(string description, string title = "Error")
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Error, "ErrorReply", $"{(Channel as IGuildChannel)?.Guild.Name ?? "DM"} #{Channel.Name}: [{title}] {description}"));
            await Channel.SendMessageAsync(string.Empty, embed: BuildErrorEmbed(description, title));
        }
        public LogMessage LogMessage(string commandName) => new LogMessage(LogSeverity.Info, "Command", $"@{MessageAuthor.Username}#{MessageAuthor.Discriminator} in {(Channel as IGuildChannel)?.Guild.Name ?? "DM"} #{Channel.Name}: [{commandName}] {Message.Content}");

        private static Embed BuildErrorEmbed(string description, string title = "Error")
        {
            var embed = new EmbedBuilder
            {
                Title = title,
                Description = description,
                Color = new Color(244, 67, 54),
                Footer = new EmbedFooterBuilder
                {
                    Text = "If you believe this should not have happened, please submit a bug report"
                }
            };

            return embed;
        }

        private static Embed BuildErrorEmbed(Exception error) => BuildErrorEmbed(error.Message, $"Error - {error.GetType().Name}");
    }
}
