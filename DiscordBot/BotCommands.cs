using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public static class BotCommands
    {
        public static List<Command> Commands = new List<Command>();

        public static void AddCommand(string names, string description, string parameters, Command.Context context, Action<Message, List<string>> action)
        {
            Commands.Add(new Command(names.Split('|'), action, description, parameters.Length > 0 ? parameters.Split(';').ToList() : new List<string>(), context));
        }

        public static void Load()
        {
            Random random = new Random();

            AddCommand("!help|!man", "Lists all commands and their descriptions or one command and its description", "~command", Command.Context.All, (message, args) =>
            {
                if (args.Count > 0)
                {
                    if (!args[0].StartsWith("!")) args[0] = '!' + args[0];
                    Command c = null;
                    foreach (Command command in Commands)
                    {
                        if (args[0].StartsWithAny(command.Names))
                        {
                            c = command;
                            break;
                        }
                    }
                    if (c == null)
                    {
                        message.Reply("*That command does not exist*", 5000);
                    }
                    else
                    {
                        message.Reply($"`{c.NamesString}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}", 60000);
                    }
                }
                else
                {
                    string s = "*This message will delete itself in 60 seconds*\n\n";
                    foreach (Command c in Commands)
                    {
                        if ((c.CommandContext == Command.Context.OwnerOnly && message.User.Id == Config.OwnerId) || c.CommandContext == Command.Context.All || DiscordBot.GetMessageContext(message) == c.CommandContext)
                            s += $"`{c.NamesString}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}\n\n";
                    }

                    message.Reply(s, 60000);
                }

            });
            AddCommand("!echo|!say", "Repeats <message> back to you", "message", Command.Context.All, (message, args) =>
            {
                message.Reply(message.Text.Substring(5).Trim());
            });
            AddCommand("!hello|!test", "Says hi", "", Command.Context.All, new Action<Message, List<string>>((message, args) =>
            {
                message.Reply("Hello! :hand_splayed:");
            }));
            AddCommand("!8ball", "It knows your future", "yes or no question", Command.Context.All, (message, args) =>
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

                message.Reply($"<@{message.User.Id}>: ***{args[0]}***\n" + responses[random.Next(responses.Length)]);
            });
            AddCommand("!poll", "Starts a poll with the given comma-separated options", "length (minutes);option1;option2;~option3;~option4...", Command.Context.GuildChannel, async (message, args) =>
            {
                double minutes = 0;
                if (!double.TryParse(args[0], out minutes) || minutes < 0.01 || minutes > 1440) throw new BotCommandException("Please specify a valid positive integer number of minutes >= 0.01 and <= 1440.");

                string[] voteOptions = args.Skip(1).Join().Split(',');
                if (voteOptions.Length < 2) throw new BotCommandException("Polls must have at least two options. Don't force things upon people. It's not nice.");

                Poll p = Poll.Create(message.Channel, message.User, message);

                if (p == null) throw new BotCommandException("There is already a poll in progress");

                foreach (string option in voteOptions) p.Options.Add(new PollOption(option.TrimStart()));

                string messageToSend = $"***<@{message.User.Id}> has started a poll with the following options:***\n";
                foreach (PollOption o in p.Options) messageToSend += $"{p.Options.IndexOf(o) + 1}: {o.Text}\n";
                messageToSend += $"\n***Enter `!vote <number>` to vote!***\n*The poll will end in {minutes} minutes unless stopped earlier with `!endpoll`*";

                message.Reply(messageToSend);

                await Task.Delay((int)(minutes * 60000)).ContinueWith(t =>
                {
                    if (p.Active)
                    {
                        Poll.EndActive();
                    }
                });
            });
            AddCommand("!vote", "Votes in the active poll", "option number", Command.Context.GuildChannel, (message, args) =>
            {
                if (Poll.ActivePoll != null)
                {
                    if (message.User.IsBot)
                    {
                        message.Reply($"<@{message.User.Id}>: BOTs can't vote! That is called *voter fraud*.", 5000);
                        return;
                    }

                    if (Poll.ActivePoll.Voters.Contains(message.User))
                    {
                        message.Reply($"<@{message.User.Id}>: You already voted!", 5000);
                        return;
                    }

                    try
                    {
                        Poll.ActivePoll.Options[int.Parse(args[0]) - 1].Votes++;
                        message.Reply($"<@{message.User.Id}>: Vote acknowledged", 5000);
                        Poll.ActivePoll.Voters.Add(message.User);
                    }
                    catch
                    {
                        message.Reply($"<@{message.User.Id}>: Invalid poll option", 5000);
                    }
                }
                else
                {
                    message.Reply($"<@{message.User.Id}>: No poll currently in progress", 5000);
                }
            });
            AddCommand("!endpoll", "Ends the currently active poll", "", Command.Context.GuildChannel, (message, args) =>
            {
                if (Poll.ActivePoll != null)
                {
                    Poll.EndActive();
                }
                else
                {
                    message.Reply($"@{message.User.Name}: No poll currently in progress", 5000);
                }
            });
            AddCommand("!username", "Sets the bot's username", "username", Command.Context.OwnerOnly, async (message, args) =>
            {
                await DiscordBot.Client.CurrentUser.Edit(username: args.Join());
                message.Reply($"*Username updated to {args.Join()}*");
            });
            AddCommand("!quote", "Stores quotes and provides methods of searching for them", "<id>|add <quote text>|remove <quote ID>|edit <quote ID> <new text>|find [query]|random|?", Command.Context.GuildChannel, (message, args) =>
            {
                int quoteId = 0;

                if (!File.Exists($"{Config.BasePath}quotes.{message.Server.Id}.txt"))
                    File.Create($"{Config.BasePath}quotes.{message.Server.Id}.txt").Close();

                if (args[0] == "?" || args[0] == "random")
                {
                    string[] quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt");
                    if (quotes.Length > 0)
                    {
                        int id = random.Next(quotes.Length);
                        if (!string.IsNullOrWhiteSpace(quotes[id]))
                            message.Reply($"#{id}: *{quotes[id].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*");
                        else throw new BotCommandException("This server has no quotes in its quotes list. Add one using `!quote add <quote text>`");
                    }
                }
                else if (args[0] == "remove")
                {
                    if (args.Count < 2 || args[1] == "help" || !args[1].IsInteger())
                    {
                        message.Reply("`!quote remove <quote ID>`\nRemoves a quote from the server's quotes list");
                        return;
                    }

                    List<string> quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt").ToList();
                    int id = Convert.ToInt32(args[1]);

                    quotes[id] = "";

                    File.WriteAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt", quotes);
                    message.Reply($"Deleted quote {id}");
                }
                else if (args[0] == "add")
                {
                    if (args.Count < 2 || args[1] == "help")
                    {
                        message.Reply("`!quote add <quote text>`\nAdds a quote to the server's quotes list");
                        return;
                    }

                    File.AppendAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt", new[] { args.Skip(1).Join().Replace("\\n", "\\\\n").Replace("\n", "\\n") });
                    string[] quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt");
                    message.Reply($"Added quote *{args.Skip(1).Join()}* with quote ID {quotes.Length - 1}");
                }
                else if (args[0] == "find")
                {
                    string[] quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt");
                    List<string> quotesWithPhrase = new List<string>();

                    string searchTerm = args.Skip(1).Join();

                    for (int i = 0; i < quotes.Length; i++)
                    {
                        if (quotes[i].ToLower().Contains(searchTerm.ToLower()) && !string.IsNullOrWhiteSpace(quotes[i]))
                            quotesWithPhrase.Add($"{i}: *{quotes[i].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*");
                    }

                    message.Reply($"**Quotes Matching *\"{searchTerm}\"***\n{string.Join("\n", quotesWithPhrase)}");
                }
                else if (args[0] == "edit")
                {
                    if (args.Count < 3 || args[1] == "help" || !args[1].IsInteger())
                    {
                        message.Reply("`!quote edit <quote ID> <new text>`\nEdits an existing quote by ID");
                        return;
                    }

                    List<string> quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt").ToList();
                    int id = Convert.ToInt32(args[1]);

                    quotes[id] = args.Skip(2).Join().Replace("\\n", "\\\\n").Replace("\n", "\\n");

                    File.WriteAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt", quotes);
                    message.Reply($"Updated quote {id} with new text *{args.Skip(2).Join()}*");
                }
                else if (int.TryParse(args[0], out quoteId))
                {
                    if (args.Count < 2 || args[1] == "help" || !args[1].IsInteger())
                    {
                        message.Reply("`!quote <id>`\nGets a quote by ID");
                        return;
                    }
                    string[] quotes = File.ReadAllLines($"{Config.BasePath}quotes.{message.Server.Id}.txt");
                    if (quotes.Length >= quoteId + 1 && quoteId >= 0 && !string.IsNullOrWhiteSpace(quotes[quoteId]))
                    {
                        message.Reply($"*{quotes[quoteId].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*");
                    }
                    else throw new BotCommandException("No quote found for that ID");
                }
                else
                {
                    message.Reply("`!quote <<id>|add <quote text>|remove <quote ID>|edit <quote ID> <new text>|find [query]|random|?>`", 7500);
                }
            });
            AddCommand("!talk|!t|!chat", "Chats with Cleverbot", "message", Command.Context.All, async (message, args) =>
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://cleverbot.io/1.0/ask");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string jsonToSend = JsonConvert.SerializeObject(
                        new
                        {
                            user = Config.CleverBotUsername,
                            key = Config.CleverBotKey,
                            nick = "DiscordUser." + DiscordBot.Client.CurrentUser.Id,
                            text = args.Join()
                        });

                    streamWriter.Write(jsonToSend);
                    await streamWriter.FlushAsync();
                    streamWriter.Close();
                }

                var json = JObject.Parse("{status: \"No response found\"}");
                var httpResponse = (HttpWebResponse)(await httpWebRequest.GetResponseAsync());
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    json = JObject.Parse(result);
                }

                if (json["status"].ToString() != "success")
                {
                    message.Reply(json["status"].ToString());
                }
                else
                {
                    message.Reply(json["response"].ToString());
                }
            });
            AddCommand("!gameinfo|!gamedata", "Lists how long a person has played games on a given date", $"username;~date", Command.Context.GuildChannel, (message, args) =>
            {
                DateTime date = DateTime.Today;

                if (args.Count == 0 || (args.Count > 1 && !DateTime.TryParse(args[1], out date)))
                    throw new CommandSyntaxException("!gameinfo");

                User user;
                try
                {
                    if (!args[0].Contains('#'))
                    {
                        var choices = message.Server.FindUsers(args[0]);
                        if (choices.Count() == 1) user = choices.First();
                        else if (choices.Count() > 1) throw new BotCommandException("There are multiple users with that username. Please include the DiscordTag (ex. #1242) to specify");
                        else throw new BotCommandException("No users in this server matched that username");
                    }
                    else
                    {
                        user = (from u in message.Server.FindUsers(args[0].Split('#')[0]) where u.Discriminator == ushort.Parse(args[0].Split('#')[1]) select u).First();
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new BotCommandException("User not found");
                }

                var gamer = Gamer.FindById(user.Id);

                if (user.CurrentGame.HasValue)
                {
                    var time = Gamer.GameStopped(user, user.CurrentGame.Value.Name);
                    DiscordBot.LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is no longer playing {user.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours", DiscordBot.EventType.GameUpdated);
                    Gamer.GameStarted(user);
                    DiscordBot.LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is now playing {user.CurrentGame.Value.Name}", DiscordBot.EventType.GameUpdated);
                }
                gamer = Gamer.FindById(user.Id);

                if (gamer == null || !gamer.GamesPlayed.ContainsKey(date) || gamer.GamesPlayed[date].Count == 0)
                    throw new BotCommandException("The user played no games on that UTC date");

                string output = $"**Games played by {gamer.Username} on {date.ToShortDateString()} (UTC) (H:MM):**\n";
                foreach (var data in gamer.GamesPlayed[date])
                    output += $"{data.Game}: {(int)data.TimePlayed.TotalHours}:{data.TimePlayed.Minutes.ToString("D2")}\n";

                message.Reply(output);
            });
            AddCommand("!sendfile", "Sends various bot files to the bot owner", "gamedata|log <date>|quotes|reminders", Command.Context.OwnerOnly, async (message, args) =>
            {
                if (args[0] == "gamedata") await message.Channel.SendFile(Config.GameDataPath);
                else if (args[0] == "log") await message.Channel.SendFile(Config.LogDirectoryPath + args[1] + ".log");
                else if (args[0] == "aliases") await message.Channel.SendFile($"{Config.BasePath}aliases.{message.Server.Id}.txt");
                else if (args[0] == "quotes") await message.Channel.SendFile($"{Config.BasePath}quotes.{message.Server.Id}.txt");
                else if (args[0] == "reminders") await message.Channel.SendFile(Config.RemindersPath);
                else throw new BotCommandException("Please enter a valid file to send");
            });
            AddCommand("!ggez", "Replies with a random Overwatch 'gg ez' replacement", "", Command.Context.All, (message, args) =>
            {
                string[] responses = new string[] {
                        "It's past my bedtime. Please don't tell my mommy.",
                        "C'mon, Mom! One more game before you tuck me in. Oops mistell.",
                        "Mommy says people my age shouldn't suck their thumbs.",
                        "For glory and honor! Huzzah comrades!",
                        "I could really use a hug right now.",
                        "Ah shucks... you guys are the best!",
                        "Great game, everyone!",
                        "Well played. I salute you all.",
                        "I'm trying to be a nicer person. It's hard, but I am trying, guys.",
                        "I feel very, very small... please hold me...",
                        "It was an honor to play with you all. Thank you.",
                        "I'm wrestling with some insecurity issues in my life but thank you all for playing with me.",
                        "Gee whiz! That was fun. Good playing!",
                        "Wishing you all the best."
                    };

                message.Reply(responses[random.Next(responses.Length)]);
            });
            AddCommand("!remind", "Sends a message to a user when they come online", "username", Command.Context.GuildChannel, (message, args) =>
            {
                User user;
                try
                {
                    if (!args[0].Contains('#'))
                    {
                        var choices = message.Server.FindUsers(args[0]);
                        if (choices.Count() == 1) user = choices.First();
                        else if (choices.Count() > 1) throw new BotCommandException("There are multiple users with that username. Please include the DiscordTag (ex. #1242) to specify");
                        else throw new BotCommandException("No users in this server matched that username");
                    }
                    else
                    {
                        user = (from u in message.Server.FindUsers(args[0].Split('#')[0]) where u.Discriminator == ushort.Parse(args[0].Split('#')[1]) select u).First();
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new BotCommandException("User not found");
                }

                if (!File.Exists(Config.RemindersPath))
                {
                    File.Create(Config.RemindersPath).Close();
                    File.WriteAllText(Config.RemindersPath, JsonConvert.SerializeObject(new Dictionary<ulong, List<string>>()));
                }

                var reminders = JsonConvert.DeserializeObject<Dictionary<ulong, List<string>>>(File.ReadAllText(Config.RemindersPath));

                if (reminders != null && reminders.ContainsKey(user.Id))
                {
                    reminders[user.Id].Add($"Reminder from <@{message.User.Id}>: " + args.Skip(1).Join());
                }
                else
                {
                    if (reminders == null) reminders = new Dictionary<ulong, List<string>>();
                    reminders.Add(user.Id, new List<string> { $"Reminder from <@{message.User.Id}>: " + args.Skip(1).Join() });
                }

                File.WriteAllText(Config.RemindersPath, JsonConvert.SerializeObject(reminders));

                message.Reply("Reminder saved");
            });
            AddCommand("!byid", "Looks up a Discord message by its ID number", "message ID;~channel mention", Command.Context.All, async (message, args) =>
            {
                ulong id;
                if (args.Count < 1 || (args.Count == 2 && message.MentionedChannels.Count() < 1) || !ulong.TryParse(args[0], out id)) throw new BotCommandException("Please supply a valid Discord message ID and channel combination");

                Message quotedMessage;
                if (message.MentionedChannels.Count() < 1)
                    quotedMessage = (await message.Channel.DownloadMessages(1, id, Relative.Around, false))[0];
                else
                    quotedMessage = (await message.MentionedChannels.First().DownloadMessages(1, id, Relative.Around, false))[0];

                string response = $"*On {quotedMessage.Timestamp.ToShortDateString()} at {quotedMessage.Timestamp.ToShortTimeString()} UTC @{quotedMessage.User.Name} said:*\n{quotedMessage.RawText}";

                message.Reply(response.Length > 2000 ? response.Substring(0, 2000) : response);
            });
            AddCommand("!back", "Creates a backronym from the provided letters", "letters (start with & for extended dictionary);~count", Command.Context.All, (message, args) =>
            {
                Random rand = new Random();
                if (args.Count < 1 || args[0].Length > 25) throw new BotCommandException("Please provide a string from which to make a backronym that is 25 or fewer characters.");

                int count = 1;
                if (args.Count > 1) int.TryParse(args[1], out count);
                count = count < 1 ? 1 : count > 10 ? 10 : count;

                string backronym = "";
                bool useComplex = false;
                for (int i = 0; i < count; i++)
                {
                    backronym += (args[0].StartsWith("&") ? args[0].Substring(1).ToUpper() : args[0].ToUpper()) + ": ";
                    foreach (char c in args[0])
                    {
                        if (c == '&' && c == args[0][0])
                            useComplex = true;

                        if (File.Exists($"{(useComplex ? "" : "simple")}words\\{char.ToLower(c)}.txt"))
                        {
                            string[] words = File.ReadAllLines($"{(useComplex ? "" : "simple")}words\\{char.ToLower(c)}.txt");
                            string word = words[rand.Next(words.Length)];
                            if (word.Length > 1)
                                backronym += char.ToUpper(word[0]) + word.Substring(1) + " ";
                            else
                                backronym += char.ToUpper(word[0]) + " ";
                        }
                    }

                    backronym += "\n";
                }

                message.Reply(backronym);
            });

            Commands = Commands.OrderBy(c => c.NamesString).ToList();

        }
    }

    public static class CommandHelperExtensionMethods
    {
        public static string Join(this IEnumerable<string> values)
        {
            return string.Join(" ", values.ToArray());
        }

        public static bool IsInteger(this string s)
        {
            int temp = 0;
            return int.TryParse(s, out temp);
        }

        public static bool IsDouble(this string s)
        {
            double temp = 0;
            return double.TryParse(s, out temp);
        }
    }
}
