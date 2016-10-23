using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Command
    {
        public string Text;
        public string Usage;
        public string HelpDescription;
        public Context CommandContext;

        public Action<Message> Action;

        public Command(string text, Action<Message> action, string description, string usage = "", Context context = Context.All)
        {
            Text = text;
            Usage = usage;
            HelpDescription = description;
            Action = action;
            CommandContext = context;
        }

        public enum Context
        {
            All,
            GuildChannel,
            DirectMessage,
            OwnerOnly
        }
    }
}
