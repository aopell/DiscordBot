using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("dynamic-messages.json")]
    public class DynamicMessages : Config
    {
        public List<DynamicMessageInfo> Messages { get; set; }

        public DynamicMessages()
        {
            Messages = new List<DynamicMessageInfo>();
        }
    }

    public class DynamicMessageInfo
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public ulong UpdateInterval { get; set; }
        public string CommandText { get; set; }
    }
}
