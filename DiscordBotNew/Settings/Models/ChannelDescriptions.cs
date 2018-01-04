using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    public class ChannelDescriptions : Config
    {
        public Dictionary<ulong, string> Descriptions { get; set; }
    }
}
