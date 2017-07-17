using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandResult
{
    public class ErrorResult : ICommandResult
    {
        public string Message { get; set; }
        public string Title { get; set; }
        public Exception Exception { get; set; }

        public ErrorResult(string message, string title = "Error")
        {
            Message = message;
            Title = title;
            Exception = null;
        }

        public ErrorResult(Exception ex)
        {
            Message = ex.Message;
            Title = $"Error - {ex.GetType().Name}";
            Exception = ex;
        }

        public Embed GenerateEmbed()
        {
            var builder = new EmbedBuilder
            {
                Title = Title,
                Description = Message,
                Color = new Color(244, 67, 54),
                Footer = new EmbedFooterBuilder
                {
                    Text = "If you believe this should not have happened, please submit a bug report"
                }
            };

            return builder;
        }

        public override string ToString() => $"**{Title}**\n{Message}";
    }
}
