using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("reminders.json")]
    public class UserReminders : Config
    {
        public List<ReminderInfo> Reminders { get; set; }
    }

    public class ReminderInfo
    {
        public ulong SenderId { get; set; }
        public ulong ReceiverId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }

        // Backwards compatibility with ValueTuple reminder format
        [JsonProperty("Item1")]
        private ulong item1
        {
            set => SenderId = value;
        }
        [JsonProperty("Item2")]
        private ulong item2
        {
            set => ReceiverId = value;
        }
        [JsonProperty("Item3")]
        private DateTimeOffset item3
        {
            set => Timestamp = value;
        }
        [JsonProperty("Item4")]
        private string item4
        {
            set => Message = value;
        }

        public ReminderSlim Slim() => this;
    }

    public class ReminderSlim
    {
        public ulong S { get; set; }
        public ulong R { get; set; }
        public DateTimeOffset T { get; set; }
        public string M { get; set; }

        public static implicit operator ReminderSlim(ReminderInfo r) => new ReminderSlim
        {
            S = r.SenderId,
            R = r.ReceiverId,
            T = r.Timestamp,
            M = r.Message
        };

        public static implicit operator ReminderInfo(ReminderSlim r) => new ReminderInfo
        {
            SenderId = r.S,
            ReceiverId = r.R,
            Timestamp = r.T,
            Message = r.M
        };
    }
}
