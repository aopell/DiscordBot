using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader.CommandContext;

namespace DiscordBotNew.CommandLoader
{
    public static class PaginatedCommand
    {
        public static Regex FooterRegex = new Regex("(?<command>.*?) \\| \\d+-\\d+ of \\d+ \\| Page (?<pageNumber>\\d+) of (?<lastPage>\\d+)");

        private static EmbedBuilder GeneratePaginatedEmbed(string command, List<KeyValuePair<string, string>> content, int pageNumber, int pageSize, EmbedBuilder template = null)
        {
            if (template == null)
            {
                template = new EmbedBuilder();
            }

            template.Footer = template.Footer ?? new EmbedFooterBuilder();
            template.Footer.Text = $"{command} | {(pageNumber - 1) * pageSize + 1}-{Math.Min(content.Count, pageSize * pageNumber)} of {content.Count} | Page {pageNumber} of {Math.Ceiling(content.Count / (float)pageSize)}";

            foreach (var f in content.Skip((pageNumber - 1) * pageSize).Take(pageSize))
            {
                template.AddField(f.Key, f.Value);
            }

            return template;
        }

        public static async Task SendPaginatedMessage(DiscordMessageContext context, string command, List<KeyValuePair<string, string>> content, int pageNumber, int pageSize, EmbedBuilder template = null)
        {
            IUserMessage message;
            if (context is DiscordPaginatedMessageContext p)
            {
                await p.Reply("", embed: GeneratePaginatedEmbed(command, content, pageNumber, pageSize, template));
                message = p.Message;
            }
            else
            {
                message = await context.Channel.SendMessageAsync("", embed: GeneratePaginatedEmbed(command, content, pageNumber, pageSize, template));
            }

            await message.RemoveAllReactionsAsync();

            if (pageNumber > 2)
            {
                await message.AddReactionAsync(new Emoji("⏮"));
            }

            if (pageNumber > 1)
            {
                await message.AddReactionAsync(new Emoji("⬅"));
            }

            if (pageNumber < Math.Ceiling(content.Count / (float)pageSize))
            {
                await message.AddReactionAsync(new Emoji("➡"));
            }

            if (pageNumber < Math.Ceiling(content.Count / (float)pageSize) - 1)
            {
                await message.AddReactionAsync(new Emoji("⏭"));
            }
        }
    }
}
