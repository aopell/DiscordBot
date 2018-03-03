using System;
using System.Collections.Generic;
using System.Text;

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
        private ulong item1
        {
            set => SenderId = value;
        }
        private ulong item2
        {
            set => ReceiverId = value;
        }
        private DateTimeOffset item3
        {
            set => Timestamp = value;
        }
        private string item4
        {
            set => Message = value;
        }
    }
}
