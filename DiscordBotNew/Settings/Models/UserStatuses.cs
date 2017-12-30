using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    class UserStatuses : ConfigModel
    {
        public Dictionary<ulong, UserStatusInfo> Statuses { get; set; }
    }

    public class UserStatusInfo
    {
        public DateTimeOffset LastOnline { get; set; }
        public DateTimeOffset StatusLastChanged { get; set; }
        public string Game { get; set; }
        public DateTimeOffset? StartedPlaying { get; set; }
        public DateTimeOffset LastMessageSent { get; set; }
    }
}
