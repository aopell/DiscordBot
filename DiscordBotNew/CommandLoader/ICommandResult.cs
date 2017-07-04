using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBotNew.CommandLoader
{
    public interface ICommandResult
    {
        string Message { get; set; }
    }
}
