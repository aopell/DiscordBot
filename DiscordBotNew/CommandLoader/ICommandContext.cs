using System.Threading.Tasks;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public interface ICommandContext
    {
        DiscordSocketClient BotClient { get; }

        Task Reply(string message);
        Task ReplyError(string message, string title);
    }
}