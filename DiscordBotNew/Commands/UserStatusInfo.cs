using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.Commands
{
    public class UserStatusInfo
    {
        public DateTimeOffset LastOnline { get; set; }
        public DateTimeOffset StatusLastChanged { get; set; }
        public string Game { get; set; }
        public DateTimeOffset? StartedPlaying { get; set; }
    }
}
