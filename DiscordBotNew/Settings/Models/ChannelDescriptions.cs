using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    public class ChannelDescriptions : ConfigModel
    {
        public Dictionary<ulong, string> Descriptions { get; set; }
    }
}
