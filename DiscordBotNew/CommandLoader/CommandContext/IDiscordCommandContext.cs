using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{

    public interface IDiscordCommandContext : ICommandContext
    {
        IMessageChannel Channel { get; }
    }

    public interface IDiscordGuildChannelContext : IDiscordCommandContext
    {
        new IGuildChannel Channel { get; }
        IGuild Guild { get; }
    }
}
