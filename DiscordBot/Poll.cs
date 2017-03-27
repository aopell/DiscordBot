using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBot
{
    public class Poll
    {
        public int Id = polls.Count > 0 ? polls.Last().Value.Id + 1 : 1;
        public List<PollOption> Options;
        public DateTime StartTime;
        public double Length;
        public bool Active;
        public bool Anonymous;
        public Channel Channel;
        public User Creator;
        public int TotalVotes
        {
            get
            {
                int votes = 0;
                foreach (PollOption o in Options)
                {
                    votes += o.Votes.Count;
                }
                return votes;
            }
        }
        public List<User> Voters;
        private static Dictionary<ulong, Poll> polls = new Dictionary<ulong, Poll>();

        public Poll(Channel channel, User creator, double length, bool anonymous, Message message = null)
        {
            StartTime = DateTime.Now;
            Active = true;
            Channel = channel;
            Options = new List<PollOption>();
            Creator = creator;
            Voters = new List<User>();
            Length = length;
            Anonymous = anonymous;
        }

        public static Poll Create(Channel channel, User creator, Message message, double length, bool anonymous)
        {
            if (polls.ContainsKey(channel.Id)) return null;

            Poll poll = new Poll(channel, creator, length, anonymous, message);
            polls.Add(channel.Id, poll);
            return poll;
        }

        public static void End(Channel c)
        {
            if (polls.ContainsKey(c.Id))
            {
                var p = polls[c.Id];
                if (p.Active)
                {
                    p.Active = false;
                    var winners = p.GetWinners();
                    string messageToSend = "======== POLL RESULTS ========\n";
                    foreach (PollOption o in winners) messageToSend += $"**{o.Text} ({o.Votes.Count} {(o.Votes.Count == 1 ? "vote" : "votes")}){(p.Anonymous ? "**" : $":** {string.Join(", ", (from v in o.Votes select v.Nickname ?? v.Name))}")}\n";
                    foreach (PollOption o in p.Options) if (!winners.Contains(o)) messageToSend += $"{o.Text} ({o.Votes.Count} votes){(p.Anonymous ? "" : $": {string.Join(", ", (from v in o.Votes select v.Nickname ?? v.Name))}")}\n";
                    messageToSend += $"({p.TotalVotes} {(p.TotalVotes == 1 ? "total vote" : "total votes")})";
                    p.Channel.Reply(messageToSend);
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

        public static Poll GetPoll(Channel c)
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
    }

    public class PollOption
    {
        public string Text;
        public List<User> Votes;

        public PollOption(string text)
        {
            Text = text;
            Votes = new List<User>();
        }
    }
}
