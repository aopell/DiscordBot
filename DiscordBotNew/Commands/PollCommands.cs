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
        [Command("poll"), HelpText("Prints the channel's active poll"), CommandScope(ChannelType.Text)]
        public static ICommandResult GetPoll(SocketMessage message)
        {
            Poll po;
            if ((po = Poll.GetPoll(message.Channel)) != null)
            {
                return new SuccessResult("", embed: po.GetEmbed(message));
            }
            else
            {
                return new ErrorResult("There is no currently active poll in this channel");
            }
        }

        [Command("poll"), HelpText("Starts a poll with the given length (in minutes) and the given options"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> StartPoll(SocketMessage message, double length, string option1, string option2, [JoinRemainingParameters] string[] otherOptions = null)
        {
            var options = new List<string> { option1, option2 };
            if (otherOptions != null) options.AddRange(otherOptions);
            return await Poll.CreatePoll(message, length, options, false);
        }

        [Command("anonpoll"), HelpText("Starts an anonymous poll with the given length (in minutes) and the given options"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> StartAnonPoll(SocketMessage message, double length, string option1, string option2, [JoinRemainingParameters] string[] otherOptions = null)
        {
            var options = new List<string> { option1, option2 };
            if (otherOptions != null) options.AddRange(otherOptions);
            return await Poll.CreatePoll(message, length, options, true);
        }

        [Command("vote"), HelpText("Votes in the channel's active poll"), CommandScope(ChannelType.Text)]
        public static ICommandResult Vote(SocketMessage message, [HelpText("option number|option text"), JoinRemainingParameters]string option)
        {
            Poll p = Poll.GetPoll(message.Channel);
            if (p != null && !p.Anonymous && p.Active)
            {
                bool update = false;
                if (message.Author.IsBot)
                {
                    return new ErrorResult("BOTs can't vote in polls");
                }

                if (p.Voters.ContainsKey(message.Author))
                {
                    update = true;
                    if (p.MinutesLeft < 0.5)
                    {
                        return new ErrorResult("Too late to change your vote! Sorry.");
                    }
                }

                bool num = true;
                if (int.TryParse(option, out int i) && i > 0 && i <= p.Options.Count)
                {
                    p.Voters[message.Author] = p.Options[i - 1];
                }
                else if (p.Options.Any(x => x.Text == option))
                {
                    num = false;
                    if (p.Options.Count(x => x.Text == option) == 1)
                    {
                        p.Voters[message.Author] = p.Options.First(x => x.Text == option);
                    }
                    else
                    {
                        return new ErrorResult("There are multiple options with the same text. Please vote by number instead.");
                    }
                }
                else
                {
                    return new ErrorResult("That poll option doesn't exist");
                }

                return new SuccessResult(!update ? $"<@{message.Author.Id}>: Vote for {(num ? "option #" : "'")}{option}{(num ? "" : "'")} acknowledged" : $"<@{message.Author.Id}>: Vote update to {(num ? "option #" : "'")}{option}{(num ? "" : "'")} acknowledged");
            }
            else
            {
                if (p != null && p.Anonymous)
                    return new ErrorResult($"The current poll is anonymous. Please use `!anonvote {p.Id} <number|option>` in a direct message to me to vote.");
                return new ErrorResult("No poll currently in progress");
            }
        }

        [Command("anonvote"), HelpText("Anonymously votes in the anonymous poll with the provided ID number"), CommandScope(ChannelType.DM)]
        public static async Task<ICommandResult> AnonVote(SocketMessage message, [HelpText("poll ID")]int pollId, [HelpText("option number|option text"), JoinRemainingParameters]string option)
        {
            Poll p = Poll.GetPollById(pollId);

            if (p != null && p.Active)
            {
                if (await p.Channel.GetUserAsync(message.Author.Id) == null)
                {
                    return new ErrorResult("That poll doesn't exist");
                }

                bool update = false;
                if (message.Author.IsBot)
                {
                    return new ErrorResult($"BOTs can't vote in polls");
                }


                if (p.Voters.ContainsKey(message.Author))
                {
                    update = true;
                    if (p.MinutesLeft < 0.5)
                    {
                        return new ErrorResult("Too late to change your vote! Sorry.");
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
                        {
                            p.Voters[message.Author] = p.Options.First(x => x.Text == option);
                        }
                        else
                        {
                            return new ErrorResult("There are multiple options with the same text. Please vote by number instead.");
                        }
                    }
                    else
                    {
                        return new ErrorResult("That poll option doesn't exist");
                    }

                    return new SuccessResult(!update ? $"<@{message.Author.Id}>: Vote for {(num ? "option #" : "'")}{option}{(num ? "" : "'")} acknowledged" : $"<@{message.Author.Id}>: Vote update to {(num ? "option #" : "")}{option}{(num ? "" : "'")} acknowledged");
                }
                catch (Exception ex)
                {
                    return new ErrorResult(ex);
                }
            }
            else
            {
                return new ErrorResult("No poll with that id currently in progress");
            }
        }

        [Command("endpoll"), HelpText("Ends the channel's active poll"), CommandScope(ChannelType.Text)]
        public static ICommandResult EndPoll(SocketMessage message)
        {
            Poll p = Poll.GetPoll(message.Channel);
            if (p != null && p.Active)
            {
                return Poll.End(message.Channel);
            }
            return new ErrorResult("No poll currently in progress");
        }
    }
}
