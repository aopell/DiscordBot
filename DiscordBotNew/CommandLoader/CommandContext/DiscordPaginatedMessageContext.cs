using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

namespace DiscordBotNew.CommandLoader.CommandContext
{
    public class DiscordPaginatedMessageContext : DiscordMessageContext
    {
        public ScrollDirection Direction { get; }
        public string Command { get; }
        public int CurrentPageNumber { get; }
        public int LastPage { get; }

        public int UpdatedPageNumber
        {
            get
            {
                switch (Direction)
                {
                    case ScrollDirection.SkipToBeginning:
                        return 1;
                    case ScrollDirection.Backwards:
                        return Math.Max(1, CurrentPageNumber - 1);
                    case ScrollDirection.Forwards:
                        return Math.Min(CurrentPageNumber + 1, LastPage);
                    case ScrollDirection.SkipToEnd:
                        return LastPage;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public DiscordPaginatedMessageContext(IEmote emote, IUserMessage message, DiscordBot bot) : base(message, bot)
        {
            switch (emote.Name)
            {
                case "⏮":
                    Direction = ScrollDirection.SkipToBeginning;
                    break;
                case "⬅":
                    Direction = ScrollDirection.Backwards;
                    break;
                case "➡":
                    Direction = ScrollDirection.Forwards;
                    break;
                case "⏭":
                    Direction = ScrollDirection.SkipToEnd;
                    break;
            }

            Match match = PaginatedCommand.FooterRegex.Match(message.Embeds.FirstOrDefault()?.Footer?.Text ?? "");
            Command = match.Groups["command"].Value;
            CurrentPageNumber = int.Parse(match.Groups["pageNumber"].Value);
            LastPage = int.Parse(match.Groups["lastPage"].Value);
        }

        public override async Task Reply(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            await Message.ModifyAsync(
            properties =>
            {
                properties.Content = message;
                properties.Embed = embed;
            });
        }

        public override async Task Reply(string message) => await Reply(message, false);

        public override async Task ReplyError(Exception ex) => await ReplyError(ex.Message, ex.GetType().Name);

        public override async Task ReplyError(string description, string title = "Error")
        {
            await Message.ModifyAsync(
            properties =>
            {
                properties.Content = "";
                properties.Embed = BuildErrorEmbed(description, title);
            });
        }

        public enum ScrollDirection
        {
            SkipToBeginning,
            Backwards,
            Forwards,
            SkipToEnd
        }
    }
}
