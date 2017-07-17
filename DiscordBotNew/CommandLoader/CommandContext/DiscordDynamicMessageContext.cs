using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public class DiscordDynamicMessageContext : DiscordMessageContext
    {
        public string CommandText { get; }

        public DiscordDynamicMessageContext(IUserMessage message, DiscordBot bot, string commandText) : base(message, bot)
        {
            CommandText = commandText;
        }

        public override async Task Reply(string message) => await Reply(message, false);

        public override async Task Reply(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "DynMsg", $"{Guild?.Name ?? "DM"} #{Channel.Name}: {message}"));
            await Message.ModifyAsync(
            msg =>
            {
                msg.Content = message;
                msg.Embed = embed;
            }, options);
        }

        public override async Task ReplyError(string description, string title)
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "DynMsg", $"{Guild?.Name ?? "DM"} #{Channel.Name}: [{title}] {description}"));
            await Message.ModifyAsync(msg => msg.Embed = BuildErrorEmbed(description, title));
        }

        public override async Task ReplyError(Exception ex) => await ReplyError(ex.Message, ex.GetType().Name);

        public override LogMessage LogMessage(string commandName) => new LogMessage(LogSeverity.Info, "Command", $"Dynamic message {Message.Id}  #{Channel.Name}: [{commandName}] {CommandText}");

    }
}
