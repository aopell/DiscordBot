using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    public class BotSettings : Config
    {
        public string Token { get; set; }
        public string GrammarToken { get; set; }
        public ulong OwnerId { get; set; }
        public string CommandPrefix { get; set; }
        public string Timezone { get; set; }
    }
}
