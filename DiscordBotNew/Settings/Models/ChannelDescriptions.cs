using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("descriptions.json")]
    public class ChannelDescriptions : Config
    {
        public Dictionary<ulong, string> Descriptions { get; set; }

        public ChannelDescriptions()
        {
            Descriptions = new Dictionary<ulong, string>();
        }
    }
}
