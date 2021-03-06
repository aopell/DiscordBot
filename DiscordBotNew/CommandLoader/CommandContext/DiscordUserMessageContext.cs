﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public class DiscordUserMessageContext : DiscordMessageContext
    {
        public DiscordUserMessageContext(IUserMessage message, DiscordBot bot) : base(message, bot) { }

        public override async Task Reply(string message) => await Reply(message, false);
        public override async Task Reply(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "Reply", $"{Guild?.Name ?? "DM"} #{Channel.Name}: {message}"));

            if (embed == null)
            {
                const int maxMessageLength = 1800;
                foreach (string m in Enumerable.Range(0, message.Length / maxMessageLength + 1).Select(i => message.Substring(i * maxMessageLength, message.Length - i * maxMessageLength > maxMessageLength ? maxMessageLength : message.Length - i * maxMessageLength)))
                {
                    await Channel.SendMessageAsync(m, isTTS, null, options);
                }
            }
            else
            {
                await Channel.SendMessageAsync(message, isTTS, embed, options);
            }
        }
    }
}
