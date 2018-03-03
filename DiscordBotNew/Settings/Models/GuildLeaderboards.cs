using System;
using System.Collections.Generic;
using System.Text;
using DiscordBotNew.Commands;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("leaderboards.json")]
    public class GuildLeaderboards : Config
    {
        public Dictionary<ulong, Leaderboard> Leaderboards { get; set; }
    }
}
