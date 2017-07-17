using System;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public interface ICommandContext
    {
        DiscordBot Bot { get; }
        LogMessage LogMessage(string commandName);

        Task Reply(string message);
        Task ReplyError(string message, string title);

        Task ReplyError(Exception ex);
    }
}