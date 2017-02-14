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
        public string[] Names;
        public List<string> Parameters;
        public string HelpDescription;
        public Context CommandContext;
        public Action<Message, List<string>> Action;

        public int RequiredParameters
        {
            get
            {
                return Parameters.Where(x => (!x.StartsWith("~"))).Count();
            }
        }

        public string NamesString
        {
            get
            {
                return string.Join("|", Names);
            }
        }

        public string Usage
        {
            get
            {
                string usage = "";
                foreach (string parameter in Parameters)
                {
                    if (parameter.StartsWith("~"))
                    {
                        usage += $"[{parameter.Substring(1)}] ";
                    }
                    else
                    {
                        usage += $"<{parameter}> ";
                    }
                }

                return usage;
            }
        }

        public Command(string[] names, Action<Message, List<string>> action, string description, List<string> parameters, Context context = Context.All)
        {
            Names = names;
            Parameters = parameters;
            HelpDescription = description;
            Action = action;
            CommandContext = context;
        }

        public enum Context
        {
            All,
            GuildChannel,
            DirectMessage,
            OwnerOnly,
            DeletePermission
        }
    }

    public class BotCommandException : Exception
    {
        public BotCommandException(string message) : base(message)
        {

        }
    }

    public class CommandSyntaxException : Exception
    {
        public CommandSyntaxException(string message) : base($"The syntax of the command was not valid. Use \"!help {message}\" for more information")
        {

        }
    }
}
