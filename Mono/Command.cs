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

        public Action<Message> Action;

        public Command(string text, Action<Message> action, string description, string usage = "")
        {
            Text = text;
            Usage = usage;
            HelpDescription = description;
            Action = action;
        }
    }
}
