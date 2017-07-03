using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;
using DiscordBotNew;

namespace DiscordBotNew.Commands
{
    public static class PollCommands
    {
        [Command("poll"), HelpText("Starts a poll with the given length (in minutes) and the given options"), CommandScope(ChannelType.Text)]
        public static async void StartPoll(SocketMessage message, double length, string option1, string option2, [JoinRemainingParameters] string[] otherOptions = null)
        {
            var options = new List<string> { option1, option2 };
            if (otherOptions != null) options.AddRange(otherOptions);
            await Poll.CreatePoll(message, length, options, false);
        }

        [Command("anonpoll"), HelpText("Starts an anonymous poll with the given length (in minutes) and the given options"), CommandScope(ChannelType.Text)]
        public static async void StartAnonPoll(SocketMessage message, double length, string option1, string option2, [JoinRemainingParameters] string[] otherOptions = null)
        {
            var options = new List<string> { option1, option2 };
            if (otherOptions != null) options.AddRange(otherOptions);
            await Poll.CreatePoll(message, length, options, true);
        }

        [Command("vote"), HelpText("Votes in the channel's active poll"), CommandScope(ChannelType.Text)]
        public static async void Vote(SocketMessage message, [HelpText("option number | option text"), JoinRemainingParameters]string option)
        {
            Poll p = Poll.GetPoll(message.Channel);
            if (p != null && !p.Anonymous && p.Active)
            {
                bool update = false;
                if (message.Author.IsBot)
                {
                    await message.ReplyError($"BOTs can't vote in polls");
                    return;
                }

                if (p.Voters.ContainsKey(message.Author))
                {
                    update = true;
                    if (p.MinutesLeft < 0.5)
                    {
                        await message.ReplyError("Too late to change your vote! Sorry.");
                        return;
                    }
                }

                try
                {
                    bool num = true;
                    if (int.TryParse(option, out int i) && i > 0 && i <= p.Options.Count)
                    {
                        p.Voters[message.Author] = p.Options[i - 1];
                    }
                    else if (p.Options.Any(x => x.Text == option))
                    {
                        num = false;
                        if (p.Options.Count(x => x.Text == option) == 1)
                            p.Voters[message.Author] = p.Options.First(x => x.Text == option);
                        else await message.ReplyError("There are multiple options with the same text. Please vote by number instead.");
                    }
                    else await message.ReplyError("That poll option doesn't exist");

                    await message.Reply(!update ? $"<@{message.Author.Id}>: Vote for {(num ? "option #" : "")}{option}{(num ? "" : "'")} acknowledged" : $"<@{message.Author.Id}>: Vote update to {(num ? "option #" : "'")}{option}{(num ? "" : "'")} acknowledged");
                }
                catch (Exception ex)
                {
                    await message.ReplyError(ex);
                }
            }
            else
            {
                if (p != null && p.Anonymous)
                    await message.ReplyError($"The current poll is anonymous. Please use `!anonvote {p.Id} <number|option>` in a direct message to me to vote.");
                else
                    await message.ReplyError("No poll currently in progress");
            }
        }
    }
}
