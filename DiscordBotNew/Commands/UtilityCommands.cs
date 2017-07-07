using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew.Commands
{
    public static class UtilityCommands
    {
        [Command("date"), HelpText("Gets the current date")]
        public static ICommandResult Date(ICommandContext context, [DisplayName("Windows TimeZone ID")] string timezone = "Pacific Standard Time", [HelpText("See C# DateTime's String.Format method")] string format = "f") => new SuccessResult(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, timezone).DateTime.ToString(format));
    }
}
