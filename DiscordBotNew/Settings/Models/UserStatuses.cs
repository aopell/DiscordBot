using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("statuses.json")]
    public class UserStatuses : Config
    {
        public Dictionary<ulong, UserStatusInfo> Statuses { get; set; }

        public UserStatuses()
        {
            Statuses = new Dictionary<ulong, UserStatusInfo>();
        }
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
