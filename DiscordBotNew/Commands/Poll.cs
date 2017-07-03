using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew
{
    public class Poll
    {
        public byte Id;
        public List<PollOption> Options;
        public DateTime StartTime;
        public double Length;
        public bool Active;
        public bool Anonymous;
        public double MinutesLeft => Math.Round((TimeSpan.FromMinutes(Length) - (DateTime.Now - StartTime)).TotalMinutes, 1);
        public ISocketMessageChannel Channel;
        public SocketUser Creator;
        public int TotalVotes => Voters.Count;

        public Dictionary<SocketUser, PollOption> Voters;
        private static Dictionary<ulong, Poll> polls = new Dictionary<ulong, Poll>();
        private static Dictionary<byte, Poll> pollsById = new Dictionary<byte, Poll>();

        public Poll(ISocketMessageChannel channel, SocketUser creator, double length, bool anonymous, SocketMessage message = null)
        {
            StartTime = DateTime.Now;
            Active = true;
            Channel = channel;
            Options = new List<PollOption>();
            Creator = creator;
            Voters = new Dictionary<SocketUser, PollOption>();
            Length = length;
            Anonymous = anonymous;
        }

        public static Poll Create(ISocketMessageChannel channel, SocketUser creator, SocketMessage message, double length, bool anonymous)
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

        public static void End(ISocketMessageChannel c)
        {
            if (polls.ContainsKey(c.Id))
            {
                var p = polls[c.Id];
                if (p.Active)
                {
                    p.Active = false;
                    var winners = p.GetWinners();
                    string messageToSend = winners.Aggregate("======== POLL RESULTS ========\n", (current, o) => current + $"**{o.Text} ({o.Votes.Count} {(o.Votes.Count == 1 ? "vote" : "votes")}){(p.Anonymous ? "**" : $":** {string.Join(", ", (from v in o.Votes select (v as IGuildUser)?.Nickname ?? v.Username))}")}\n");
                    messageToSend = p.Options.OrderByDescending(x => x.Votes.Count).Where(o => !winners.Contains(o)).Aggregate(messageToSend, (current, o) => current + $"{o.Text} ({o.Votes.Count} votes){(p.Anonymous ? "" : $": {string.Join(", ", (from v in o.Votes select (v as IGuildUser)?.Nickname ?? v.Username))}")}\n");
                    messageToSend += $"({p.TotalVotes} {(p.TotalVotes == 1 ? "total vote" : "total votes")})";
                    p.Channel.SendMessageAsync(messageToSend);
                    polls.Remove(c.Id);
                }
            }
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

        public static Poll GetPoll(ISocketMessageChannel c)
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

        public static async Task CreatePoll(SocketMessage message, double minutes, List<string> args, bool anonymous)
        {
            if (args.Count == 0)
            {
                Poll po;
                if ((po = GetPoll(message.Channel)) != null)
                {
                    string m = $"***Currently active {(po.Anonymous ? "**anonymous **" : "")}poll started by <@{po.Creator.Id}> has the following options:***\n";
                    m = po.Options.Aggregate(m, (current, o) => current + $"{po.Options.IndexOf(o) + 1}: {o.Text}\n");
                    m += po.Anonymous ? $"\n**ONLY VOTES FROM A DIRECT MESSAGE WILL BE COUNTED!** This is **anonymous poll number #{po.Id}.** Use `!anonvote {po.Id} <number|option>`\n*The poll will end in {po.MinutesLeft} minutes unless stopped earlier with `!endpoll`*" : $"\n***Enter `!vote <number|option>` to vote!***\n*The poll will end in {po.MinutesLeft} minutes unless stopped earlier with `!endpoll`*";

                    if (po.TotalVotes > 0)
                        m += $"\n\n**ALREADY VOTED ({po.Voters.Count})** {(po.Anonymous ? "" : ": " + string.Join(", ", (from u in po.Voters select (u.Key as IGuildUser)?.Nickname ?? u.Key.Username)))}";

                    await message.Reply(m);
                }
                else
                {
                    await message.ReplyError("There is no currently active poll in this channel");
                }
                return;
            }

            if (minutes < 0.01 || minutes > 1440)
            {
                await message.ReplyError("Please specify a valid positive number of minutes >= 0.01 and <= 1440.");
                return;
            }

            Poll p = Create(message.Channel, message.Author, message, minutes, anonymous);

            if (p == null)
            {
                await message.ReplyError($"There is already a{(Poll.GetPoll(message.Channel).Anonymous ? "n anonymous" : "")} poll in progress");
                return;
            }

            foreach (string option in args) p.Options.Add(new PollOption(option.TrimStart(), p));

            string messageToSend = $"***<@{message.Author.Id}> has started {(anonymous ? "an __anonymous__ " : "a ")}poll with the following options:***\n";
            messageToSend = p.Options.Aggregate(messageToSend, (current, o) => current + $"{p.Options.IndexOf(o) + 1}: {o.Text}\n");
            messageToSend += anonymous ? $"\n**__ONLY VOTES FROM A DIRECT MESSAGE TO ME WILL BE COUNTED!__** This is **anonymous poll number #{p.Id}.**\nUse `!anonvote {p.Id} <number|option>` in a direct message to me to vote\n*The poll will end in {minutes} minutes unless stopped earlier with `!endpoll`*" : $"\n***Enter `!vote <number|option>` to vote!***\n*The poll will end in {minutes} minutes unless stopped earlier with `!endpoll`*";

            await message.Reply(messageToSend);

            await Task.Delay((int)(minutes * 60000)).ContinueWith(t =>
            {
                if (p.Active)
                {
                    End(message.Channel);
                }
            });
        }
    }

    public class PollOption
    {
        public string Text;
        private Poll poll;

        public List<SocketUser> Votes => (from v in poll.Voters where v.Value == this select v.Key).ToList();

        public PollOption(string text, Poll poll)
        {
            Text = text;
            this.poll = poll;
        }
    }
}
