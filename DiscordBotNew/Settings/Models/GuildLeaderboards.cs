using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("leaderboards.json")]
    public class GuildLeaderboards : Config
    {
        public Dictionary<ulong, LeaderboardInfo> Leaderboards { get; set; }
    }

    public class LeaderboardInfo
    {
        public ulong GuildId { get; set; }
        public int TotalMessages { get; set; }
        public Dictionary<ulong, int> ChannelMessages { get; set; }
        public Dictionary<ulong, int> UserMessages { get; set; }
        public DateTimeOffset TimeGenerated { get; set; }
    }
}
