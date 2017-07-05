using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public class DiscordChannelDescriptionContext : ICommandContext
    {
        public IGuildChannel Channel { get; }

        public DiscordChannelDescriptionContext(IGuildChannel channel)
        {
            Channel = channel;
        }

        public DiscordSocketClient BotClient { get; }
        public Task Reply(string message)
        {
            throw new NotImplementedException();
        }

        public Task ReplyError(string message, string title)
        {
            throw new NotImplementedException();
        }
    }
}
