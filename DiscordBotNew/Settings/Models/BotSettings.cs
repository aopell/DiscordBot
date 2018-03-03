using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("settings.json")]
    public class BotSettings : Config
    {
        public string Token { get; set; }
        public string GrammarToken { get; set; }
        public ulong OwnerId { get; set; }
        private ulong BotOwner
        {
            set => OwnerId = value;
        }
        public string CommandPrefix { get; set; }
        public string Timezone { get; set; }
    }
}
