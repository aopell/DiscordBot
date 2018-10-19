using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("log.json")]
    public class EventsLog : Config
    {
        public List<LogItem> Items { get; set; }
        public EventsLog()
        {
            Items = new List<LogItem>();
        }

        public void LogEvent(string text, [CallerMemberName] string method = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = -1)
        {
            Items.Add(new LogItem(text, $"{file}: {method} {(line > 0 ? $"Line {line}" : "")}".Trim()));
            SaveConfig();
        }
    }

    public class LogItem
    {
        public string Text { get; }
        public DateTimeOffset Timestamp { get; }
        public string Source { get; }

        internal LogItem(string text, string source = null)
        {
            Text = text;
            Timestamp = DateTimeOffset.Now;
            Source = source;
        }

        [JsonConstructor]
        private LogItem() { }
    }
}
