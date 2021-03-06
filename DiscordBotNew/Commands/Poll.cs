﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;

namespace DiscordBotNew
{
    public class Poll
    {
        public byte Id { get; private set; }
        public List<PollOption> Options { get; }
        public DateTimeOffset StartTime { get; }
        public double Length { get; }
        public bool Active { get; private set; }
        public bool Anonymous { get; }
        public double MinutesLeft => Math.Round((TimeSpan.FromMinutes(Length) - (DateTimeOffset.UtcNow - StartTime)).TotalMinutes, 1);
        public IMessageChannel Channel { get; }
        public IUser Creator { get; }
        public int TotalVotes => Voters.Count;

        public Dictionary<IUser, PollOption> Voters { get; }
        private static Dictionary<ulong, Poll> polls = new Dictionary<ulong, Poll>();
        private static Dictionary<byte, Poll> pollsById = new Dictionary<byte, Poll>();

        public Poll(IMessageChannel channel, IUser creator, double length, bool anonymous, IMessage message = null)
        {
            StartTime = DateTimeOffset.UtcNow;
            Active = true;
            Channel = channel;
            Options = new List<PollOption>();
            Creator = creator;
            Voters = new Dictionary<IUser, PollOption>();
            Length = length;
            Anonymous = anonymous;
        }

        public static Poll Create(IMessageChannel channel, IUser creator, IMessage message, double length, bool anonymous)
        {
            if (polls.ContainsKey(channel.Id)) return null;

            Poll poll = new Poll(channel, creator, length, anonymous, message);
            Random rand = new Random();
            byte[] id = new byte[1];
            rand.NextBytes(id);
            while (pollsById.ContainsKey(id[0]))
            {
                rand.NextBytes(id);
            }
            poll.Id = id[0];

            polls.Add(channel.Id, poll);
            pollsById.Add(poll.Id, poll);
            return poll;
        }

        public static ICommandResult End(IMessageChannel c)
        {
            if (polls.ContainsKey(c.Id))
            {
                Poll p = polls[c.Id];
                if (p.Active)
                {
                    p.Active = false;

                    var builder = new EmbedBuilder
                    {
                        Title = "Poll Results",
                        Color = new Color(33, 150, 243),
                        Footer = new EmbedFooterBuilder
                        {
                            Text = $"{p.TotalVotes} vote{(p.TotalVotes == 1 ? "" : "s")}"
                        }
                    };

                    var winners = p.GetWinners();
                    string messageToSend = winners.Aggregate("", (current, o) => current + $"**{o.Text} ({o.Votes.Count} {(o.Votes.Count == 1 ? "vote" : "votes")}){(p.Anonymous ? "**" : $":** {string.Join(", ", (from v in o.Votes select (v as IGuildUser)?.Nickname ?? v.Username))}")}\n");
                    messageToSend = p.Options.OrderByDescending(x => x.Votes.Count).Where(o => !winners.Contains(o)).Aggregate(messageToSend, (current, o) => current + $"{o.Text} ({o.Votes.Count} votes){(p.Anonymous ? "" : $": {string.Join(", ", (from v in o.Votes select (v as IGuildUser)?.Nickname ?? v.Username))}")}\n");
                    builder.Description = messageToSend;
                    polls.Remove(c.Id);
                    return new SuccessResult(embed: builder.Build());
                }
            }

            return new ErrorResult("Poll not found");
        }

        public static Poll GetPollById(int id)
        {
            try
            {
                return (from p in polls where p.Value.Id == id select p).First().Value;
            }
            catch
            {
                return null;
            }
        }

        public static Poll GetPoll(IMessageChannel c)
        {
            return polls.ContainsKey(c.Id) ? polls[c.Id] : null;
        }

        public List<PollOption> GetWinners()
        {
            int winnningAmount = 0;
            List<PollOption> options = new List<PollOption>();

            foreach (PollOption o in Options)
            {
                if (o.Votes.Count > winnningAmount)
                {
                    options.Clear();
                    options.Add(o);
                    winnningAmount = o.Votes.Count;
                }
                else if (o.Votes.Count == winnningAmount)
                {
                    options.Add(o);
                }
            }

            return options;
        }

        public static async Task<ICommandResult> CreatePoll(DiscordMessageContext context, double minutes, List<string> args, bool anonymous)
        {
            if (minutes < 0.01 || minutes > 1440)
            {
                return new ErrorResult("Please specify a valid positive number of minutes >= 0.01 and <= 1440.");
            }

            Poll p = Create(context.Channel, context.MessageAuthor, context.Message, minutes, anonymous);

            if (p == null)
            {
                return new ErrorResult($"There is already a{(Poll.GetPoll(context.Channel).Anonymous ? "n anonymous" : "")} poll in progress");
            }

            foreach (string option in args) p.Options.Add(new PollOption(option.TrimStart(), p));

            await context.Reply("", embed: p.GetEmbed(context));

            return await Task.Delay((int)(minutes * 60000)).ContinueWith(t => p.Active ? End(context.Channel) : new SuccessResult());
        }

        public Embed GetEmbed(DiscordMessageContext context)
        {
            var builer = new EmbedBuilder
            {
                Title = "Poll",
                Color = new Color(76, 175, 80),
                Description = $"Vote using `{CommandTools.GetCommandPrefix(context, context.Channel)}{(Anonymous ? $"anonvote {Id}" : "vote")} <option number|option text>`{(Anonymous ? $"\n**__ONLY VOTES FROM A DIRECT MESSAGE TO ME WILL BE COUNTED!__**\nThis is **anonymous poll number #{Id}.**" : "")}",
                Footer = new EmbedFooterBuilder
                {
                    Text = $"The poll will end in {TimeSpan.FromMinutes(MinutesLeft).ToLongString()} unless stopped earlier with '{CommandTools.GetCommandPrefix(context, context.Channel)}endpoll'"
                },
                Author = new EmbedAuthorBuilder
                {
                    Name = Creator.NicknameOrUsername(),
                    IconUrl = Creator.AvatarUrlOrDefaultAvatar()
                }
            };

            for (int i = 0; i < Options.Count; i++)
            {
                builer.AddField((i + 1).ToString(), Options[i].Text, true);
            }

            if (TotalVotes > 0)
            {
                builer.AddField("Already Voted", string.Join(", ", Voters.Select(voter => voter.Key.NicknameOrUsername())));
            }

            return builer.Build();
        }
    }

    public class PollOption
    {
        public string Text { get; }
        private readonly Poll poll;

        public List<IUser> Votes => (from v in poll.Voters where v.Value == this select v.Key).ToList();

        public PollOption(string text, Poll poll)
        {
            Text = text;
            this.poll = poll;
        }
    }
}
