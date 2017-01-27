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
        public List<PollOption> Options;
        public DateTime StartTime;
        public bool Active;
        public Channel Channel;
        public User Creator;
        public Message Message;
        public int TotalVotes
        {
            get
            {
                int votes = 0;
                foreach (PollOption o in Options)
                {
                    votes += o.Votes;
                }
                return votes;
            }
        }
        public List<User> Voters;

        public Poll(Channel channel, User creator, Message message)
        {
            StartTime = DateTime.Now;
            Active = true;
            Channel = channel;
            Options = new List<PollOption>();
            Creator = creator;
            Voters = new List<User>();
            Message = message;
        }

        private static List<Poll> Polls = new List<Poll>();
        public static Poll ActivePoll
        {
            get
            {
                foreach (Poll p in Polls)
                    if (p.Active) return p;
                return null;
            }
        }

        public static Poll Create(Channel channel, User creator, Message message)
        {
            foreach (Poll p in Polls)
            {
                if (p.Active)
                {
                    return null;
                }
            }

            Poll poll = new Poll(channel, creator, message);
            Polls.Add(poll);
            return poll;
        }

        public static async void EndActive()
        {
            foreach (Poll p in Polls)
            {
                if (p.Active)
                {
                    p.Active = false;
                    var winners = p.GetWinners();
                    string messageToSend = "__Poll Results: (Winners in **bold**)__\n";
                    foreach (PollOption o in winners) messageToSend += $"**{o.Votes} - {o.Text}**\n";
                    foreach (PollOption o in p.Options) if (!winners.Contains(o)) messageToSend += $"{o.Votes} - {o.Text}\n";
                    messageToSend += $"{p.TotalVotes} {(p.TotalVotes == 1 ? "Total Vote" : "Total Votes")}";
                    await p.Channel.SendMessage(messageToSend);
                    await p.Message.Delete();
                }
            }
        }

        public List<PollOption> GetWinners()
        {
            int winnningAmount = 0;
            List<PollOption> options = new List<PollOption>();

            foreach (PollOption o in Options)
            {
                if (o.Votes > winnningAmount)
                {
                    options.Clear();
                    options.Add(o);
                    winnningAmount = o.Votes;
                }
                else if (o.Votes == winnningAmount)
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
        public int Votes;

        public PollOption(string text)
        {
            Text = text;
            Votes = 0;
        }
    }
}
