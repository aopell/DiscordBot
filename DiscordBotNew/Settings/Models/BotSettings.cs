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
        public ulong? OwnerId { get; set; }
        [JsonProperty("botOwner")]
        private ulong BotOwner
        {
            set => OwnerId = value;
        }
        public string CommandPrefix { get; set; }
        public string Timezone { get; set; }
        public ulong? StatusMessageChannel { get; set; }
        public string StatusMessageWebhook { get; set; }
        public string Game { get; set; }
        public Dictionary<ulong, string> CustomPrefixes { get; set; }
        public ulong? StartupReplyChannel { get; set; }

        public BotSettings()
        {
            CustomPrefixes = new Dictionary<ulong, string>();
        }
    }
}
