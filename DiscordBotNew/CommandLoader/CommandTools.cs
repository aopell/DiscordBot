using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordBotNew.CommandLoader
{
    public static class CommandTools
    {
        public static Embed BuildErrorEmbed(string description, string title = "Error")
        {
            var embed = new EmbedBuilder
            {
                Title = title,
                Description = description,
                Color = new Color(244, 67, 54),
                Footer = new EmbedFooterBuilder
                {
                    Text = "If you believe this should not have happened, please submit a bug report"
                }
            };

            return embed;
        }

        public static Embed BuildErrorEmbed(Exception error) => BuildErrorEmbed(error.Message, $"Error - {error.GetType().Name}");

        public static async Task Reply(this SocketMessage m, string text, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await m.Channel.SendMessageAsync(text, isTTS, embed, options);
        }
    }
}
