using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using System.Net;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;

namespace DiscordBot
{
    public static class DiscordTools
    {
        public const string Token = "MTY5NTQ3Mjc3MzMxODU3NDA4.CtQqUQ.jPiTbmQRGYeO1ksbEQTd9LCWvuI";

        public static DiscordClient Client = new DiscordClient();

        private const string CleverBotUsername = "3G4ViNSjpAL557Ua";
        private const string CleverBotKey = "mqEJEDGPQ2vEAQQottv5nAW6U39LTPBq";

        public static Queue<Tuple<Message, string, int>> MessageQueue = new Queue<Tuple<Message, string, int>>();

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
            LogEvent("Connected to Discord as " + Client.CurrentUser.Name + "#" + Client.CurrentUser.Discriminator.ToString("D4"), EventType.Success);
            LogEvent("User ID: " + Client.CurrentUser.Id, EventType.Success);
            Client.MessageReceived += Client_MessageReceived;
            Client.UserUpdated += Client_UserUpdated;
            Client.JoinedServer += Client_JoinedServer;
            Client.MessageUpdated += Client_MessageUpdated;

            Task.Delay(200).ContinueWith((thing) =>
            {
                foreach (Server s in Client.Servers)
                    LogEvent($"Connnected to Server {s.Name}{(!string.IsNullOrEmpty(s.GetUser(Client.CurrentUser.Id).Nickname) ? $" with nickname {s.GetUser(Client.CurrentUser.Id).Nickname}" : "")}", EventType.Success);
            });

            LoadCommands();
            StartMessageQueue();
        }

        private static void Client_MessageUpdated(object sender, MessageUpdatedEventArgs e)
        {
            LogEvent($"{e.Channel.Server.Name.Shorten(15)}#{e.Channel.Name} - @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: Message updated from \"{e.Before.Text}\" to \"{e.After.Text}\"", EventType.MessageUpdated);
        }

        private static void Client_JoinedServer(object sender, ServerEventArgs e)
        {
            LogEvent("Joined new server: " + e.Server.Name, EventType.JoinedServer);
        }

        private static string lastMessage = "";

        private static void Client_UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            if (e.Before.Name != e.After.Name)
            {
                string message = $"@{e.Before.Name}#{e.Before.Discriminator.ToString("D4")} is now @{e.After.Name}#{e.After.Discriminator.ToString("D4")}";
                if (lastMessage != (lastMessage = message)) LogEvent(message, EventType.UsernameUpdated);
            }

            if (e.Before.Status != e.After.Status)
            {
                string message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} is now {e.After.Status}";
                if (lastMessage != (lastMessage = message)) LogEvent(message, EventType.StatusUpdated);
            }

            if (e.Before.CurrentGame.GetValueOrDefault(new Game("")).Name != e.After.CurrentGame.GetValueOrDefault(new Game("")).Name)
            {
                string message = "";
                string tempMessage = "";
                if (e.After.CurrentGame.HasValue && !e.Before.CurrentGame.HasValue)
                {
                    tempMessage = $"game.start.{e.After.Id}.{e.After.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} is now playing {e.After.CurrentGame.Value.Name}";
                    Gamer.GameStarted(e.After);
                }
                else if (e.Before.CurrentGame.HasValue && !e.After.CurrentGame.HasValue)
                {
                    tempMessage = $"game.stop.{e.After.Id}.{e.Before.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    var time = Gamer.GameStopped(e.After, new DiscordGame(e.Before.CurrentGame.Value.Name, e.Before.CurrentGame.Value.Type, e.Before.CurrentGame.Value.Url));
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} is no longer playing {e.Before.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours";
                }
                else if (e.Before.CurrentGame.HasValue && e.After.CurrentGame.HasValue)
                {
                    tempMessage = $"game.switch.{e.After.Id}.{e.Before.CurrentGame.Value.Name}.{e.After.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    var time = Gamer.GameStopped(e.After, new DiscordGame(e.Before.CurrentGame.Value.Name, e.Before.CurrentGame.Value.Type, e.Before.CurrentGame.Value.Url));
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} switched from playing {e.Before.CurrentGame.Value.Name} to {e.After.CurrentGame.Value.Name}  after {Math.Round(time.TotalHours, 2)} hours";
                    Gamer.GameStarted(e.After);
                }

                LogEvent(message, EventType.GameUpdated);
            }

            if (e.Before.Nickname != e.After.Nickname)
            {
                string message = "";
                if (!string.IsNullOrEmpty(e.After.Nickname))
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} is now known as {e.After.Nickname} on {e.Server.Name}";
                else
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} no longer has a nickname on {e.Server.Name}";

                if (lastMessage != (lastMessage = message)) LogEvent(message, EventType.UsernameUpdated);
            }
        }

        public static void LogEvent(string message, EventType type = EventType.MessageReceived)
        {
            if (type == EventType.Error) Console.ForegroundColor = ConsoleColor.Red;
            else if (type == EventType.BotAction) Console.ForegroundColor = ConsoleColor.DarkCyan;
            else if (type == EventType.MessageReceived) Console.ForegroundColor = ConsoleColor.Gray;
            else if (type == EventType.Success) Console.ForegroundColor = ConsoleColor.Green;
            else if (type == EventType.StatusUpdated) Console.ForegroundColor = ConsoleColor.DarkGray;
            else if (type == EventType.GameUpdated) Console.ForegroundColor = ConsoleColor.Magenta;
            else if (type == EventType.UsernameUpdated) Console.ForegroundColor = ConsoleColor.Yellow;
            else if (type == EventType.JoinedServer) Console.ForegroundColor = ConsoleColor.Cyan;
            else if (type == EventType.MessageUpdated) Console.ForegroundColor = ConsoleColor.DarkYellow;
            string logMessage = $"{DateTime.Now.ToString("[HH:mm:ss]")} {message}";
            Console.WriteLine(logMessage);
            Console.ForegroundColor = ConsoleColor.Gray;

            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            try
            {
                File.AppendAllLines($"Logs/{DateTime.Now.ToString("yyyy-MM-dd")}.log", new[] { logMessage });
            }
            catch
            {
                Task.Delay(50).ContinueWith(thing =>
                {
                    LogEvent(message, type);
                });
            }
        }

        public enum EventType
        {
            Error = -1,
            MessageReceived,
            BotAction,
            Success,
            StatusUpdated,
            GameUpdated,
            UsernameUpdated,
            JoinedServer,
            MessageUpdated
        }

        public static List<Command> Commands = new List<Command>();

        private static Random random = new Random();

        public static void LoadCommands()
        {
            Commands.Add(new Command("!help", new Action<Message>(message =>
            {
                try
                {
                    var args = GetArgs(message.Text);
                    if (args.Length > 0)
                    {
                        if (!args[0].StartsWith("!")) args[0] = '!' + args[0];
                        Command c = null;
                        foreach (Command thing in Commands) if (thing.Text == args[0]) { c = thing; break; }
                        if (c == null) message.Reply("*That command does not exist*", 5000);
                        else message.Reply($"`{c.Text}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}", 15000);
                    }
                    else
                    {
                        string s = "*This message will delete itself in 20 seconds*\n\n";
                        foreach (Command c in Commands)
                        {
                            if ((c.CommandContext == Command.Context.OwnerOnly && message.User.Id == 85877191371427840) || c.CommandContext == Command.Context.All || GetMessageContext(message) == c.CommandContext)
                                s += $"`{c.Text}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}\n\n";
                        }

                        message.Reply(s, 20000);
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Lists all commands and their descriptions or one command and its description", "[command]"));
            Commands.Add(new Command("!echo", new Action<Message>(message =>
            {
                try
                {
                    if (GetArgs(message.Text).Length < 1) throw new ParameterException("The syntax of the command was not valid. Use `!help echo` for more information");
                    string text = message.Text.Substring(5).Trim();
                    if (text.StartsWith("!echo")) text = "\\" + text;
                    message.Reply(text);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Repeats <message> back to you", "<message>"));
            Commands.Add(new Command("!hello", new Action<Message>(message =>
            {
                message.Reply("Hello! :hand_splayed:");
            }), "Says hi"));
            Commands.Add(new Command("!8ball", new Action<Message>(message =>
            {
                try
                {
                    if (GetSuffix(message.Text).Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help 8ball` for more information");
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

                    message.Reply($"<@{message.User.Id}>: ***{GetSuffix(message.Text)}***\n" + responses[random.Next(responses.Length)]);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "It knows your future", "<yes or no question>"));
            Commands.Add(new Command("!poll", new Action<Message>(async (message) =>
            {
                try
                {
                    if (GetSuffix(message.Text).Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help poll` for more information");
                    string[] voteOptions = GetSuffix(message.Text).Split(',');
                    Poll p = Poll.Create(message.Channel, message.User, message);

                    if (p == null)
                    {
                        message.Reply($"*@{message.User.Name}: Please wait, there is already a poll in progress*", 5000);
                        return;
                    }

                    foreach (string option in voteOptions) p.Options.Add(new PollOption(option));

                    string messageToSend = $"***@{message.User.Name} has started a poll with the following options:***\n";
                    foreach (PollOption o in p.Options) messageToSend += $"{p.Options.IndexOf(o) + 1}: {o.Text}\n";
                    messageToSend += "\n***Enter `!vote <number>` to vote!***\n*The poll will end in 5 minutes unless stopped earlier with `!endpoll`*";

                    message.Reply(messageToSend);

                    await Task.Delay(300000).ContinueWith(t =>
                    {
                        if (p.Active)
                        {
                            Poll.EndActive();
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Starts a poll with the given comma-separated options", "<option1>,<option2>[,option3][,option4]..."));
            Commands.Add(new Command("!vote", new Action<Message>(message =>
            {
                try
                {
                    if (Poll.ActivePoll != null)
                    {
                        if (Poll.ActivePoll.Voters.Contains(message.User))
                        {
                            message.Reply($"<@{message.User.Id}>: You already voted!", 5000);
                            return;
                        }

                        try
                        {
                            Poll.ActivePoll.Options[int.Parse(GetSuffix(message.Text)) - 1].Votes++;
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
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Votes in the active poll", "<option number>"));
            Commands.Add(new Command("!endpoll", new Action<Message>(message =>
            {
                try
                {
                    if (Poll.ActivePoll != null)
                    {
                        Poll.EndActive();
                    }
                    else
                    {
                        message.Reply($"@{message.User.Name}: No poll currently in progress", 5000);
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Ends the currently active poll"));
            Commands.Add(new Command("!username", new Action<Message>(async (message) =>
            {
                try
                {
                    if (GetSuffix(message.Text).Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help username` for more information");
                    await Client.CurrentUser.Edit(username: GetSuffix(message.Text).Shorten(32, false));
                    message.Reply($"*Username updated to {GetSuffix(message.Text)}*", 5000);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Sets the bot's username", "<username>", Command.Context.OwnerOnly));
            Commands.Add(new Command("!quote", new Action<Message>(message =>
            {
                try
                {
                    Random random = new Random();

                    string[] args = GetArgs(message.Text);
                    if (args.Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help quote` for more information");
                    int quoteId = 0;

                    if (!File.Exists($"quotes.{message.Server.Id}.txt"))
                        File.Create($"quotes.{message.Server.Id}.txt").Close();

                    if (args[0] == "?" || args[0] == "random")
                    {
                        string[] quotes = File.ReadAllLines($"quotes.{message.Server.Id}.txt");
                        if (quotes.Length > 0)
                        {
                            int id = random.Next(quotes.Length);
                            message.Reply($"#{id}: *{quotes[id]}*");
                        }
                    }
                    else if (args[0] == "add")
                    {
                        File.AppendAllLines($"quotes.{message.Server.Id}.txt", new[] { string.Join(" ", args.Skip(1)) });
                        string[] quotes = File.ReadAllLines($"quotes.{message.Server.Id}.txt");
                        message.Reply($"Added quote *{string.Join(" ", args.Skip(1))}* with quote ID {quotes.Length - 1}", 30000);
                    }
                    else if (args[0] == "find")
                    {
                        string[] quotes = File.ReadAllLines($"quotes.{message.Server.Id}.txt");
                        List<string> quotesWithPhrase = new List<string>();

                        string searchTerm = string.Join(" ", GetArgs(message.Text).Skip(1));

                        for (int i = 0; i < quotes.Length; i++)
                        {
                            if (quotes[i].ToLower().Contains(searchTerm.ToLower()))
                                quotesWithPhrase.Add($"{i}: *{quotes[i]}*");
                        }

                        message.Reply($"**Quotes Matching *\"{searchTerm}\"***\n{string.Join("\n", quotesWithPhrase)}", 30000);
                    }
                    else if (args[0] == "edit")
                    {
                        List<string> quotes = File.ReadAllLines($"quotes.{message.Server.Id}.txt").ToList();
                        int id = Convert.ToInt32(args[1]);

                        quotes[id] = string.Join(" ", args.Skip(2));

                        File.WriteAllLines($"quotes.{message.Server.Id}.txt", quotes);
                        message.Reply($"Updated quote {id} with new text *{string.Join(" ", args.Skip(2))}*", 10000);
                    }
                    else if (int.TryParse(args[0], out quoteId))
                    {
                        string[] quotes = File.ReadAllLines($"quotes.{message.Server.Id}.txt");
                        if (quotes.Length >= quoteId + 1 && quoteId >= 0)
                        {
                            message.Reply($"*{quotes[quoteId]}*", 60000);
                        }
                        else
                        {
                            message.Reply($"*No quote with ID 0*", 10000);
                        }
                    }
                    else
                    {
                        message.Reply("**!quote** [add <quote text>|edit <id> <new quote text>|id|find <search phrase>|random|?]", 7500);
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Saves quotes and provides ways to look them up", "[add <quote text>|edit <id> <new quote text>|id|find <search phrase>|random|?]", Command.Context.GuildChannel));
            Commands.Add(new Command("!chat", new Action<Message>(async (message) =>
            {
                try
                {
                    if (GetSuffix(message.Text).Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help chat` for more information");
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://cleverbot.io/1.0/ask");
                    httpWebRequest.ContentType = "application/json";
                    httpWebRequest.Method = "POST";

                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        string jsonToSend = JsonConvert.SerializeObject(
                            new
                            {
                                user = CleverBotUsername,
                                key = CleverBotKey,
                                nick = "DiscordUser." + Client.CurrentUser.Id,
                                text = GetSuffix(message.Text)
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
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Chats with Cleverbot", "<message>"));
            Commands.Add(new Command("!alias", new Action<Message>(message =>
            {
                try
                {
                    var args = GetArgs(message.Text);
                    if (args.Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help alias` for more information");

                    if (args[0] == "create")
                    {
                        string command = args[1];
                        if (!command.StartsWith("!")) command = "!" + command;
                        string text = string.Join(" ", args.Skip(2));

                        Dictionary<string, string> aliases = LoadAliases(message.Server);

                        try
                        {
                            string check = text.Split(' ')[0];
                            if (text.Contains("!echo") || aliases.ContainsKey(check))
                            {
                                message.Reply("Aliases may not contain a call to !echo or another alias", 5000);
                                return;
                            }
                        }
                        catch { }

                        if (aliases.ContainsKey(command))
                            aliases[command] = text;
                        else
                            aliases.Add(command, text);

                        string toWrite = "";
                        for (int i = 0; i < aliases.Count; i++)
                        {
                            toWrite += aliases.Keys.ToArray()[i] + "\t" + aliases.Values.ToArray()[i] + "\n";
                        }

                        File.WriteAllText($"aliases.{message.Server.Id}.txt", JsonConvert.SerializeObject(aliases));

                        message.Reply($"Successfully created alias {command} with text {text}", 5000);
                    }
                    else if (args[0] == "list")
                    {
                        string messageToSend = "";
                        var aliases = LoadAliases(message.Server);
                        if (aliases.Count > 0) messageToSend += "**Aliases:**\n\n";
                        else messageToSend = "*No aliases to show*";
                        for (int i = 0; i < aliases.Count; i++)
                        {
                            messageToSend += $"`{aliases.Keys.ToArray()[i]}`\n{aliases.Values.ToArray()[i]}\n\n";
                        }

                        message.Reply(messageToSend, 20000);
                    }
                    else if (args[0] == "remove")
                    {
                        var aliases = LoadAliases(message.Server);
                        string command = args[1];
                        if (!command.StartsWith("!")) command = '!' + command;
                        if (aliases.Remove(command))
                            message.Reply($"Successfully deleted alias `{command}`", 5000);
                        else
                            message.Reply($"Failed to delete alias `{command}`", 5000);
                        File.WriteAllText($"aliases.{message.Server.Id}.txt", JsonConvert.SerializeObject(aliases));
                    }

                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Provides ways to create, list, and remove aliases for other commands or text messages", "<create <!command> <text>|list|remove <!command>>", Command.Context.GuildChannel));
            Commands.Add(new Command("!gameinfo", new Action<Message>(message =>
            {
                try
                {
                    DateTime date = DateTime.Today;

                    string[] args = GetArgs(message.RawText);
                    if (args.Length == 0 || (args.Length > 1 && !DateTime.TryParse(args[1], out date)))
                        throw new ParameterException("The syntax of the command was not valid. Use `!help gameinfo` for more information");

                    var users = message.MentionedUsers.ToList();

                    if (users.Count <= 0) throw new ParameterException("The syntax of the command was not valid. Use `!help gameinfo` for more information");

                    var user = users[0];
                    var gamer = Gamer.FindById(user.Id);

                    if (user.CurrentGame.HasValue)
                    {
                        var time = Gamer.GameStopped(user, new DiscordGame(user.CurrentGame.Value));
                        LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is no longer playing {user.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours", EventType.GameUpdated);
                        Gamer.GameStarted(user);
                        LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is now playing {user.CurrentGame.Value.Name}", EventType.GameUpdated);
                    }
                    gamer = Gamer.FindById(user.Id);

                    if (gamer == null || !gamer.GamesPlayed.ContainsKey(date) || gamer.GamesPlayed[date].Count == 0)
                    {
                        message.Reply("*The user played no games on that date*", 5000);
                        return;
                    }


                    string output = $"**Games played by {gamer.Username} on {date.ToShortDateString()}:**\n";
                    foreach (var data in gamer.GamesPlayed[date])
                        output += $"{data.Game.Name}: {Math.Round(data.TimePlayed.TotalHours, 2)} hours\n";

                    message.Reply(output, 60000);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Lists how long people have played games", $"<@mention> [date={DateTime.Today.ToShortDateString()}]"));
            Commands.Add(new Command("!sendfile", new Action<Message>(async (message) =>
            {
                try
                {
                    string[] args = GetArgs(message.RawText);

                    if (args.Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help sendfile` for more information");
                    await message.Channel.SendFile(GetSuffix(message.RawText));
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Uploads a file at a relative path", "<path>", Command.Context.OwnerOnly));

            Commands = Commands.OrderBy(c => c.Text).ToList();
        }

        private static void LogError(Message message, Exception ex)
        {
            if (!(ex is ParameterException))
                LogEvent(ex.ToString(), EventType.Error);
            message.Reply($"*An error occurred: {ex.Message}*");
        }

        private static Dictionary<string, string> LoadAliases(Server server)
        {
            if (File.Exists($"aliases.{server.Id}.txt"))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText($"aliases.{server.Id}.txt"));
            }

            return new Dictionary<string, string>();
        }

        public static void CheckCommands(Message message)
        {
            bool ranCommand = false;
            foreach (Command c in Commands)
            {
                try
                {
                    if (message.Text.StartsWith(c.Text))
                        if (c.CommandContext == Command.Context.OwnerOnly && message.User.Id == 85877191371427840)
                        {
                            message.Channel.SendIsTyping();
                            c.Action(message);
                            ranCommand = true;
                        }
                        else if (c.CommandContext == Command.Context.All || c.CommandContext == GetMessageContext(message))
                        {
                            message.Channel.SendIsTyping();
                            c.Action(message);
                            ranCommand = true;
                        }
                }
                catch (Exception ex)
                {
                    LogEvent($"Error: {ex.ToString()}", EventType.Error);
                }
            }

            if (!ranCommand && message.Server != null)
            {
                var aliases = LoadAliases(message.Server);
                string command = message.Text.Split(' ')[0];
                if (aliases.ContainsKey(command))
                {
                    message.Delete();
                    message.Reply(aliases[command].Replace("{suffix}", GetSuffix(message.Text)));
                }
            }
        }

        private static Command.Context GetMessageContext(Message message)
        {
            if (message.Server == null)
                return Command.Context.DirectMessage;
            else
                return Command.Context.GuildChannel;
        }

        private static string GetSuffix(string text)
        {
            return string.Join(" ", text.Split(' ').Skip(1));
        }

        private static string[] GetArgs(string text)
        {
            return text.Split(' ').Skip(1).ToArray();
        }

        private static async void StartMessageQueue()
        {
            while (true)
            {
                if (MessageQueue.Count > 0)
                {
                    var message = MessageQueue.Dequeue();

                    try
                    {
                        if (message.Item3 != 0)
                            (await message.Item1.Channel.SendMessage(message.Item2)).DeleteAfterDelay(message.Item3);
                        else
                            await message.Item1.Channel.SendMessage(message.Item2);
                    }
                    catch (Exception ex)
                    {
                        LogError(message.Item1, ex);
                    }
                    await Task.Delay(200);
                }
            }
        }
    }

    public static class ExtMethods
    {
        public static void Reply(this Message message, string text, int deleteAfter = 0)
        {
            DiscordTools.MessageQueue.Enqueue(new Tuple<Message, string, int>(message, text, deleteAfter));
        }

        public static string Shorten(this string s, int maxLength, bool elipses = true)
        {
            if (s.Length > maxLength)
            {
                if (elipses) return $"{s.Substring(0, maxLength - 3).TrimEnd()}...";
                else return s.Substring(0, maxLength).TrimEnd();
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
