using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;


namespace DiscordBot
{
    public static class DiscordTools
    {
        public static string Token = "MTY5NTQ3Mjc3MzMxODU3NDA4.CtQqUQ.jPiTbmQRGYeO1ksbEQTd9LCWvuI";

        public static DiscordClient Client = new DiscordClient();

        public static void ConnectClient()
        {
            Client.ExecuteAndWait(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Client.Connect(Token, TokenType.Bot);
                        Client_Connected();
                        break;
                    }
                    catch
                    {
                        await Task.Delay(3000);
                    }
                }
            });

        }

        private static void Client_MessageReceived(object sender, MessageEventArgs e)
        {
            if (e.Message.IsAuthor)
            {
                if (e.Channel.Server != null)
                    LogEvent($"Sent \"{e.Message.Text}\" to {e.Channel.Server.Name.Shorten(15)}#{e.Channel.Name}", EventType.BotAction);
                else
                    LogEvent($"DM To @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: \"{e.Message.Text}\"", EventType.BotAction);
            }
            else
            {
                if (e.Channel.Server != null)
                    LogEvent($"{e.Channel.Server.Name.Shorten(15)}#{e.Channel.Name} - @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: {e.Message.Text}", EventType.MessageReceived);
                else
                    LogEvent($"DM From @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: {e.Message.Text}", EventType.MessageReceived);
            }
            CheckCommands(e.Message);
        }

        private static void Client_Connected()
        {
            LogEvent("Connected to discord as " + Client.CurrentUser.Name + "#" + Client.CurrentUser.Discriminator.ToString("D4"), EventType.Success);
            LogEvent("User ID: " + Client.CurrentUser.Id, EventType.Success);
            Client.MessageReceived += Client_MessageReceived;
            LoadCommands();
        }

        public static void LogEvent(string message, EventType type = EventType.MessageReceived)
        {
            if (type == EventType.Error) Console.ForegroundColor = ConsoleColor.Red;
            else if (type == EventType.BotAction) Console.ForegroundColor = ConsoleColor.DarkGray;
            else if (type == EventType.MessageReceived) Console.ForegroundColor = ConsoleColor.Gray;
            else if (type == EventType.Success) Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]")} {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public enum EventType
        {
            Error = -1,
            MessageReceived,
            BotAction,
            Success,
        }

        public static List<Command> Commands = new List<Command>();

        private static Random random = new Random();

        public static void LoadCommands()
        {
            Commands.Add(new Command("!help", new Action<Message>(async (message) =>
            {
                string s = "*This message will delete itself in 20 seconds*\n\n";
                foreach (Command c in Commands)
                {
                    s += $"`{c.Text}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}\n\n";
                }
                await message.Delete();

                Message m = await message.Channel.SendMessage(s);
                m.DeleteAfterDelay(20000);
            }), "Lists all commands and their descriptions"));
            Commands.Add(new Command("!echo", new Action<Message>(async (message) =>
            {
                await message.Delete();
                await message.Channel.SendMessage(message.Text.Substring(5).Trim());
            }), "Repeats <message> back to you", "<message>"));
            Commands.Add(new Command("!hello", new Action<Message>(async (message) =>
            {
                await message.Delete();
                await message.Channel.SendMessage("Hello! :hand_splayed:");
            }), "Says hi"));
            Commands.Add(new Command("!8ball", new Action<Message>(async (message) =>
            {
                string[] responses = new string[] {
                    "It is certain",
                    "It is decidedly so",
                    "Without a doubt",
                    "Yes, definitely",
                    "You may rely on it",
                    "As I see it, yes",
                    "Most likely",
                    "Outlook good",
                    "Yes",
                    "Signs point to yes",
                    "Reply hazy try again",
                    "Ask again later",
                    "Better not tell you now",
                    "Cannot predict now",
                    "Concentrate and ask again",
                    "Don't count on it",
                    "My reply is no",
                    "My sources say no",
                    "Outlook not so good",
                    "Very doubtful"
                };

                await message.Delete();
                await message.Channel.SendMessage($"<@{message.User.Id}>: ***{string.Join(" ", message.Text.Split(' ').Skip(1)).Trim()}***\n" + responses[random.Next(responses.Length)]);
            }), "It knows your future", "<yes or no question>"));
            Commands.Add(new Command("!poll", new Action<Message>(async (message) =>
            {
                string[] voteOptions = GetSuffix(message.Text).Split(',');
                Poll p = Poll.Create(message.Channel, message.User);

                if (p == null)
                {
                    var m = await message.Channel.SendMessage($"*@{message.User.Name}: Please wait, there is already a poll in progress*");
                    m.DeleteAfterDelay(5000);
                    return;
                }

                foreach (string option in voteOptions) p.Options.Add(new PollOption(option));

                string messageToSend = $"***@{message.User.Name} has started a poll with the following options:***\n";
                foreach (PollOption o in p.Options) messageToSend += $"{p.Options.IndexOf(o) + 1}: {o.Text}\n";
                messageToSend += "\n***Enter `!vote <number>` to vote!***\n*The poll will end in 60 seconds unless stopped earlier with `!endpoll`*";

                await message.Delete();
                await message.Channel.SendMessage(messageToSend);

                await Task.Delay(60000).ContinueWith(new Action<Task>(t =>
                {
                    if (p.Active)
                    {
                        Poll.EndActive();
                    }
                }));
            }), "Starts a poll with the given comma-separated options", "<option1>,<option2>[,option3][,option4]..."));
            Commands.Add(new Command("!vote", new Action<Message>(async (message) =>
            {
                await message.Delete();

                if (Poll.ActivePoll != null)
                {
                    if (Poll.ActivePoll.Voters.Contains(message.User))
                    {
                        (await message.Channel.SendMessage($"@{message.User.Name}: You already voted!")).DeleteAfterDelay(5000);
                        return;
                    }

                    try
                    {
                        Poll.ActivePoll.Options[int.Parse(GetSuffix(message.Text)) - 1].Votes++;
                        (await message.Channel.SendMessage($"@{message.User.Name}: Vote acknowledged")).DeleteAfterDelay(5000);
                        Poll.ActivePoll.Voters.Add(message.User);
                    }
                    catch
                    {
                        (await message.Channel.SendMessage($"@{message.User.Name}: Invalid poll option")).DeleteAfterDelay(5000);
                    }
                }
                else
                {
                    (await message.Channel.SendMessage($"<@{message.User.Name}>: No poll currently in progress")).DeleteAfterDelay(5000);
                }

            }), "Votes in the active poll", "<option number>"));
            Commands.Add(new Command("!endpoll", new Action<Message>(async (message) =>
            {
                await message.Delete();

                if (Poll.ActivePoll != null)
                {
                    Poll.EndActive();
                }
                else
                {
                    (await message.Channel.SendMessage($"@{message.User.Name}: No poll currently in progress")).DeleteAfterDelay(5000);
                }

            }), "Ends the currently active poll"));

            Commands = Commands.OrderBy(c => c.Text).ToList();
        }

        public static void CheckCommands(Message message)
        {
            foreach (Command c in Commands)
            {
                if (string.IsNullOrEmpty(c.Usage))
                {
                    if (message.Text.StartsWith(c.Text))
                    {
                        c.Action(message);
                    }
                }
                else
                {
                    if (message.Text.StartsWith(c.Text + " "))
                    {
                        c.Action(message);
                    }
                }
            }
        }

        private static string GetSuffix(string text)
        {
            return string.Join(" ", text.Split(' ').Skip(1));
        }
    }

    public static class ExtMethods
    {
        public static string Shorten(this string s, int maxLength)
        {
            if (s.Length > maxLength)
            {
                return $"{s.Substring(0, maxLength - 3).TrimEnd()}...";
            }
            return s;
        }

        public static async void DeleteAfterDelay(this Message message, int delay)
        {
            await Task.Delay(delay).ContinueWith(x => message.Delete());
            if (message.Channel.Server != null)
                DiscordTools.LogEvent($"{message.Channel.Server.Name.Shorten(15)}#{message.Channel.Name} - @{message.User.Name}#{message.User.Discriminator.ToString("D4")}: Message Deleted", DiscordTools.EventType.BotAction);
            else
                DiscordTools.LogEvent($"@{message.User.Name}#{message.User.Discriminator.ToString("D4")}: DM Deleted", DiscordTools.EventType.BotAction);
        }
    }
}
