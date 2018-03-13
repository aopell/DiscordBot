using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("countdowns.json")]
    public class GuildCountdowns : Config
    {
        public Dictionary<ulong, Dictionary<string, DateTimeOffset>> Countdowns { get; set; }
        public Dictionary<ulong, ulong> CountdownChannels { get; set; }
    }
}
