using System.Text;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandResult
{
    public class SuccessResult : ICommandResult
    {
        public string Message { get; set; }
        public bool IsTTS { get; set; }
        public bool ReplyIfPossible { get; set; }
        public AllowedMentions AllowedMentions { get; set; }
        public Embed Embed { get; set; }
        public RequestOptions Options { get; set; }
        public bool HasContent { get; set; }

        public SuccessResult()
        {
            HasContent = false;
        }

        public SuccessResult(string message = "", bool isTTS = false, Embed embed = null, RequestOptions options = null, bool replyIfPossible = false, AllowedMentions allowedMentions = null)
        {
            Message = message;
            IsTTS = isTTS;
            Embed = embed;
            Options = options;
            HasContent = true;
            ReplyIfPossible = replyIfPossible;
            AllowedMentions = allowedMentions;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Message);
            builder.AppendLine();
            if (Embed != null)
            {
                if (!string.IsNullOrWhiteSpace(Embed.Author?.Name))
                {
                    builder.AppendLine($"**{Embed.Author.Value.Name}**");
                }
                if (!string.IsNullOrWhiteSpace(Embed.Title))
                {
                    builder.AppendLine($"**{Embed.Title}**");
                }
                if (!string.IsNullOrWhiteSpace(Embed.Description))
                {
                    builder.AppendLine(Embed.Description);
                }
                foreach (var field in Embed.Fields)
                {
                    builder.AppendLine($"**{field.Name}**");
                    builder.AppendLine(field.Value);
                }
                if (!string.IsNullOrWhiteSpace(Embed.Footer?.Text))
                {
                    builder.AppendLine(Embed.Footer.Value.Text);
                }
            }

            return builder.ToString();
        }
    }
}
