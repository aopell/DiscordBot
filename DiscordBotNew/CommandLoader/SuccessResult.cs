using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader
{
    public class SuccessResult : ICommandResult
    {
        public string Message { get; set; }
        public bool IsTTS { get; set; }
        public Embed Embed { get; set; }
        public RequestOptions Options { get; set; }
        public bool HasContent { get; set; }

        public SuccessResult()
        {
            HasContent = false;
        }

        public SuccessResult(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            Message = message;
            IsTTS = isTTS;
            Embed = embed;
            Options = options;
            HasContent = true;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Message);
            builder.AppendLine();
            builder.AppendLine($"**{Embed.Title}**");
            builder.AppendLine(Embed.Description);
            foreach (var field in Embed.Fields)
            {
                builder.AppendLine($"**{field.Name}**");
                builder.AppendLine(field.Value);
            }

            return builder.ToString();
        }
    }
}
