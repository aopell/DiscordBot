using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public class DiscordChannelDescriptionContext : IDiscordGuildChannelContext
    {
        public IGuildChannel Channel { get; }
        public IGuild Guild => Channel.Guild;
        IMessageChannel IDiscordCommandContext.Channel => Channel as IMessageChannel;

        public string FullCommand { get; }
        public DiscordBot Bot { get; }

        public DiscordChannelDescriptionContext(string command, IGuildChannel channel, DiscordBot bot)
        {
            FullCommand = command;
            Channel = channel;
            Bot = bot;
        }

        public Task Reply(string message)
        {
            return Task.CompletedTask;
        }

        public Task ReplyError(string message, string title)
        {
            return Task.CompletedTask;
        }

        public Task ReplyError(Exception ex) => ReplyError(ex.Message, ex.GetType().Name);

        public LogMessage LogMessage(string commandName) => new LogMessage(LogSeverity.Info, "Command", $"Channel description of {Channel.Guild.Name}  #{Channel.Name}: [{commandName}] {FullCommand}");
    }
}
