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

        public static async Task ReplyError(this SocketMessage m, string description, string title = "Error")
        {
            await m.Channel.SendMessageAsync(string.Empty, embed: BuildErrorEmbed(description, title));
        }

        public static async Task ReplyError(this SocketMessage m, Exception error) => await m.ReplyError(error.Message, $"Error - {error.GetType().Name}");

        public static ChannelType GetChannelType(this SocketMessage m)
        {
            switch (m.Channel)
            {
                case IGroupChannel group:
                    return ChannelType.Group;
                case IDMChannel dm:
                    return ChannelType.DM;
                default:
                    return ChannelType.Text;
            }
        }
    }
}
