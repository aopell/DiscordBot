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

        public UserReminders()
        {
            Reminders = new List<ReminderInfo>();
        }
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
    }
}
