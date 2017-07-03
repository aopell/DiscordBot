using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBotNew.CommandLoader
{
    public class CommandAttribute : Attribute
    {
        public string[] Names { get; }

        public CommandAttribute(params string[] names)
        {
            Names = names.Select(s => s.ToLower()).ToArray();
        }
    }

    public class HelpTextAttribute : Attribute
    {
        public string Text { get; }

        public HelpTextAttribute(string text)
        {
            Text = text;
        }
    }

    public class RequiredAttribute : Attribute
    {

    }

    public class JoinRemainingParametersAttribute : Attribute
    {

    }
}
