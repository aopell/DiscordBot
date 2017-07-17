using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public class DiscordUserMessageContext : DiscordMessageContext
    {
        public DiscordUserMessageContext(IUserMessage message, DiscordBot bot) : base(message, bot) { }
    }
}
