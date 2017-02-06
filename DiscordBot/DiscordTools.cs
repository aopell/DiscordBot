using System;
using System.Collections.Generic;
using System.Linq;
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

        public static DiscordClient Client;
        public const string BasePath = "D:\\home\\data\\jobs\\continuous\\DiscordBot\\";

        private const string CleverBotUsername = "3G4ViNSjpAL557Ua";
        private const string CleverBotKey = "mqEJEDGPQ2vEAQQottv5nAW6U39LTPBq";

        public static void ConnectClient()
        {
            DiscordConfigBuilder b = new DiscordConfigBuilder();
            b.MessageCacheSize = 0;
            Client = new DiscordClient(b);

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
            else if (e.Channel.Server == null)
            {
                LogEvent($"DM From @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: {e.Message.Text}", EventType.MessageReceived);
            }
            CheckCommands(e.Message);
        }

        private static async void Client_Connected()
        {
            LogEvent("Connected to Discord as " + Client.CurrentUser.Name + "#" + Client.CurrentUser.Discriminator.ToString("D4"), EventType.Success);
            LogEvent("User ID: " + Client.CurrentUser.Id, EventType.Success);
            Client.MessageReceived += Client_MessageReceived;
            Client.UserUpdated += Client_UserUpdated;
            Client.JoinedServer += Client_JoinedServer;
            Client.MessageUpdated += Client_MessageUpdated;

            await Task.Delay(200).ContinueWith((thing) =>
            {
                foreach (Server s in Client.Servers)
                    LogEvent($"Connnected to Server {s.Name}{(!string.IsNullOrEmpty(s.GetUser(Client.CurrentUser.Id).Nickname) ? $" with nickname {s.GetUser(Client.CurrentUser.Id).Nickname}" : "")}", EventType.Success);
            });

            await (await Client.CreatePrivateChannel(85877191371427840)).SendMessage($"{DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]")} Now online!");

            LoadCommands();
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
                if (lastMessage != (lastMessage = message))
                {
                    LogEvent(message, EventType.StatusUpdated);

                    if (e.Before.Status != UserStatus.Online && e.After.Status == UserStatus.Online)
                    {
                        var reminders = JsonConvert.DeserializeObject<Dictionary<ulong, List<string>>>(File.ReadAllText($"{BasePath}reminders.json"));
                        if (reminders.ContainsKey(e.After.Id))
                        {
                            foreach (string s in reminders[e.After.Id])
                            {
                                e.After.SendMessage(s);
                            }

                            reminders.Remove(e.After.Id);
                            File.WriteAllText($"{BasePath}reminders.json", JsonConvert.SerializeObject(reminders));
                        }
                    }
                }
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
                    var time = Gamer.GameStopped(e.After, e.Before.CurrentGame.Value.Name);
                    message = $"@{e.After.Name}#{e.After.Discriminator.ToString("D4")} is no longer playing {e.Before.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours";
                }
                else if (e.Before.CurrentGame.HasValue && e.After.CurrentGame.HasValue)
                {
                    tempMessage = $"game.switch.{e.After.Id}.{e.Before.CurrentGame.Value.Name}.{e.After.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    var time = Gamer.GameStopped(e.After, e.Before.CurrentGame.Value.Name);
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
            string logMessage = $"{DateTime.Now.ToString("[HH:mm:ss]")} [{type.ToString()}] {message}";
            Console.WriteLine(logMessage);
            Console.ForegroundColor = ConsoleColor.Gray;

            try
            {
                if (!Directory.Exists($"{BasePath}Logs"))
                    Directory.CreateDirectory($"{DiscordTools.BasePath}Logs");
                File.AppendAllLines($"{BasePath}Logs\\{DateTime.Now.ToString("yyyy-MM-dd")}.log", new[] { logMessage });
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
            Commands.Add(new Command("!help|!man", new Action<Message>(message =>
            {
                try
                {
                    var args = GetArgs(message.Text);
                    if (args.Length > 0)
                    {
                        if (!args[0].StartsWith("!")) args[0] = '!' + args[0];
                        Command c = null;
                        foreach (Command thing in Commands) if (args[0].StartsWithAny(thing.Text.Split('|'))) { c = thing; break; }
                        if (c == null) message.Reply("*That command does not exist*", 5000);
                        else message.Reply($"`{c.Text}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}", 60000);
                    }
                    else
                    {
                        string s = "*This message will delete itself in 60 seconds*\n\n";
                        foreach (Command c in Commands)
                        {
                            if ((c.CommandContext == Command.Context.OwnerOnly && message.User.Id == 85877191371427840) || c.CommandContext == Command.Context.All || GetMessageContext(message) == c.CommandContext)
                                s += $"`{c.Text}{(string.IsNullOrEmpty(c.Usage) ? "" : $" {c.Usage}")}`\n{c.HelpDescription}\n\n";
                        }

                        message.Reply(s, 60000);
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Lists all commands and their descriptions or one command and its description", "[command]"));
            Commands.Add(new Command("!echo|!say", new Action<Message>(message =>
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
            Commands.Add(new Command("!hello|!test", new Action<Message>(message =>
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

                    string[] args = GetArgs(message.Text);

                    double minutes = 0;
                    if (!double.TryParse(args[0], out minutes) || minutes < 0.01 || minutes > 1440) throw new ParameterException("Please specify a valid positive integer number of minutes >= 0.01 and <= 1440.");

                    string[] voteOptions = string.Join(" ", args.Skip(1)).Split(',');
                    if (voteOptions.Length < 2) throw new ParameterException("Polls must have at least two options. Don't force things upon people. It's not nice.");

                    Poll p = Poll.Create(message.Channel, message.User, message);

                    if (p == null) throw new ParameterException("There is already a poll in progress");

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
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Starts a poll with the given comma-separated options", "<length (minutes)> <option1>,<option2>[,option3][,option4]...", Command.Context.GuildChannel));
            Commands.Add(new Command("!vote", new Action<Message>((Message message) =>
            {
                try
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
            }), "Votes in the active poll", "<option number>", Command.Context.GuildChannel));
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

            }), "Ends the currently active poll", context: Command.Context.GuildChannel));
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

                    if (!File.Exists($"{BasePath}quotes.{message.Server.Id}.txt"))
                        File.Create($"{BasePath}quotes.{message.Server.Id}.txt").Close();

                    if (args[0] == "?" || args[0] == "random")
                    {
                        string[] quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt");
                        if (quotes.Length > 0)
                        {
                            int id = random.Next(quotes.Length);
                            if (!string.IsNullOrWhiteSpace(quotes[id]))
                                message.Reply($"#{id}: *{quotes[id].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*");
                            else message.Reply("*A quote with that ID does not exist.*");
                        }
                    }
                    else if (args[0] == "remove")
                    {
                        List<string> quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt").ToList();
                        int id = Convert.ToInt32(args[1]);

                        quotes[id] = "";

                        File.WriteAllLines($"{BasePath}quotes.{message.Server.Id}.txt", quotes);
                        message.Reply($"Deleted quote {id}", 5000);
                    }
                    else if (args[0] == "add")
                    {
                        File.AppendAllLines($"{BasePath}quotes.{message.Server.Id}.txt", new[] { string.Join(" ", args.Skip(1)).Replace("\\n", "\\\\n").Replace("\n", "\\n") });
                        string[] quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt");
                        message.Reply($"Added quote *{string.Join(" ", args.Skip(1))}* with quote ID {quotes.Length - 1}", 30000);
                    }
                    else if (args[0] == "find")
                    {
                        string[] quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt");
                        List<string> quotesWithPhrase = new List<string>();

                        string searchTerm = string.Join(" ", GetArgs(message.Text).Skip(1));

                        for (int i = 0; i < quotes.Length; i++)
                        {
                            if (quotes[i].ToLower().Contains(searchTerm.ToLower()) && !string.IsNullOrWhiteSpace(quotes[i]))
                                quotesWithPhrase.Add($"{i}: *{quotes[i].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*");
                        }

                        message.Reply($"**Quotes Matching *\"{searchTerm}\"***\n{string.Join("\n", quotesWithPhrase)}", 30000);
                    }
                    else if (args[0] == "edit")
                    {
                        List<string> quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt").ToList();
                        int id = Convert.ToInt32(args[1]);

                        quotes[id] = string.Join(" ", args.Skip(2)).Replace("\\n", "\\\\n").Replace("\n", "\\n");

                        File.WriteAllLines($"{BasePath}quotes.{message.Server.Id}.txt", quotes);
                        message.Reply($"Updated quote {id} with new text *{string.Join(" ", args.Skip(2))}*", 10000);
                    }
                    else if (int.TryParse(args[0], out quoteId))
                    {
                        string[] quotes = File.ReadAllLines($"{BasePath}quotes.{message.Server.Id}.txt");
                        if (quotes.Length >= quoteId + 1 && quoteId >= 0 && !string.IsNullOrWhiteSpace(quotes[quoteId]))
                        {
                            message.Reply($"*{quotes[quoteId].Replace("\\n", "\n").Replace("\\\\n", "\\n")}*", 60000);
                        }
                        else
                        {
                            message.Reply($"*No quote with ID 0*", 10000);
                        }
                    }
                    else
                    {
                        message.Reply("**!quote** [add <quote text>|remove <id>|edit <id> <new quote text>|remove <id>|id|find <search phrase>|random|?]", 7500);
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Saves quotes and provides ways to look them up", "[add <quote text>|edit <id> <new quote text>|id|find <search phrase>|random|?]", Command.Context.GuildChannel));
            Commands.Add(new Command("!chat|!t", new Action<Message>(async (message) =>
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

                        File.WriteAllText($"{BasePath}aliases.{message.Server.Id}.txt", JsonConvert.SerializeObject(aliases));

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
                        File.WriteAllText($"{BasePath}aliases.{message.Server.Id}.txt", JsonConvert.SerializeObject(aliases));
                    }

                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Provides ways to create, list, and remove aliases for other commands or text messages", "<create <!command> <text>|list|remove <!command>>", Command.Context.GuildChannel));
            Commands.Add(new Command("!gameinfo|!gamedata", new Action<Message>((message) =>
            {
                try
                {
                    DateTime date = DateTime.Today;

                    string[] args = GetArgs(message.RawText);
                    if (args.Length == 0 || (args.Length > 1 && !DateTime.TryParse(args[1], out date)))
                        throw new ParameterException("The syntax of the command was not valid. Use `!help gameinfo` for more information");

                    User user;
                    try
                    {
                        if (!args[0].Contains('#'))
                        {
                            var choices = message.Server.FindUsers(args[0]);
                            if (choices.Count() == 1) user = choices.First();
                            else throw new ParameterException("There are multiple users with that username. Please include the DiscordTag (ex. #1242) to specify");
                        }
                        else
                        {
                            user = (from u in message.Server.FindUsers(args[0].Split('#')[0]) where u.Discriminator == ushort.Parse(args[0].Split('#')[1]) select u).First();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        throw new ParameterException("User not found");
                    }

                    var gamer = Gamer.FindById(user.Id);

                    if (user.CurrentGame.HasValue)
                    {
                        var time = Gamer.GameStopped(user, user.CurrentGame.Value.Name);
                        LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is no longer playing {user.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours", EventType.GameUpdated);
                        Gamer.GameStarted(user);
                        LogEvent($"@{user.Name}#{user.Discriminator.ToString("D4")} is now playing {user.CurrentGame.Value.Name}", EventType.GameUpdated);
                    }
                    gamer = Gamer.FindById(user.Id);

                    if (gamer == null || !gamer.GamesPlayed.ContainsKey(date) || gamer.GamesPlayed[date].Count == 0)
                        throw new ParameterException("The user played no games on that UTC date");

                    string output = $"**Games played by {gamer.Username} on {date.ToShortDateString()} (UTC) (H:MM):**\n";
                    foreach (var data in gamer.GamesPlayed[date])
                        output += $"{data.Game}: {(int)data.TimePlayed.TotalHours}:{data.TimePlayed.Minutes.ToString("D2")}\n";

                    message.Reply(output);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Lists how long people have played games", $"<Username[#DiscordTag]> [date={DateTime.Today.ToShortDateString()}]"));
            Commands.Add(new Command("!sendfile", new Action<Message>(async (message) =>
            {
                try
                {
                    string[] args = GetArgs(message.RawText);

                    if (args[0] == "gamedata") await message.Channel.SendFile(BasePath + "gamedata-new.json");
                    else if (args[0] == "log") await message.Channel.SendFile(BasePath + "Logs\\" + args[1] + ".log");
                    else if (args[0] == "aliases") await message.Channel.SendFile(BasePath + $"aliases.{message.Server.Id}.txt");
                    else if (args[0] == "quotes") await message.Channel.SendFile(BasePath + $"quotes.{message.Server.Id}.txt");
                    else if (args[0] == "reminders") await message.Channel.SendFile(BasePath + $"reminders.json");
                    else throw new ParameterException("Please enter a valid file to send");

                    if (args.Length == 0) throw new ParameterException("The syntax of the command was not valid. Use `!help sendfile` for more information");

                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }

            }), "Uploads a file at a relative path", "<gamedata|log <date>|aliases|quotes>", Command.Context.OwnerOnly));
            Commands.Add(new Command("!ggez", new Action<Message>(message =>
            {
                try
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
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Replies with a random Overwatch 'gg ez' replacement"));
            Commands.Add(new Command("!remind", new Action<Message>((Message message) =>
            {
                try
                {
                    var args = GetArgs(message.Text);
                    if (args.Length < 2) throw new ParameterException("The syntax of the command was not valid. Use `!help remind` for more information");

                    User user;
                    try
                    {
                        if (!args[0].Contains('#'))
                        {
                            var choices = message.Server.FindUsers(args[0]);
                            if (choices.Count() == 1) user = choices.First();
                            else throw new ParameterException("There are multiple users with that username. Please include the DiscordTag (ex. #1242) to specify");
                        }
                        else
                        {
                            user = (from u in message.Server.FindUsers(args[0].Split('#')[0]) where u.Discriminator == ushort.Parse(args[0].Split('#')[1]) select u).First();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        throw new ParameterException("User not found");
                    }

                    if (!File.Exists($"{BasePath}reminders.json"))
                    {
                        File.Create($"{BasePath}reminders.json").Close();
                        File.WriteAllText($"{BasePath}reminders.json", JsonConvert.SerializeObject(new Dictionary<ulong, List<string>>()));
                    }

                    var reminders = JsonConvert.DeserializeObject<Dictionary<ulong, List<string>>>(File.ReadAllText($"{BasePath}reminders.json"));

                    if (reminders != null && reminders.ContainsKey(user.Id))
                    {
                        reminders[user.Id].Add($"Reminder from <@{message.User.Id}>: " + string.Join(" ", args.Skip(1)));
                    }
                    else
                    {
                        if (reminders == null) reminders = new Dictionary<ulong, List<string>>();
                        reminders.Add(user.Id, new List<string> { $"Reminder from <@{message.User.Id}>: " + string.Join(" ", args.Skip(1)) });
                    }

                    File.WriteAllText($"{BasePath}reminders.json", JsonConvert.SerializeObject(reminders));

                    message.Reply("Reminder saved");
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Sends a message to a user when they come online", "<Username[#Discriminator]> <Message>"));
            Commands.Add(new Command("!getmessage|!byid", new Action<Message>(async (Message message) =>
            {
                try
                {
                    string[] args = GetArgs(message.Text);

                    ulong id;
                    if (args.Length < 1 || (args.Length == 2 && message.MentionedChannels.Count() < 1) || !ulong.TryParse(args[0], out id)) throw new ParameterException("Please supply a valid Discord message ID and channel combination");

                    Message mm;
                    if (message.MentionedChannels.Count() < 1)
                        mm = (await message.Channel.DownloadMessages(1, id, Relative.Around, false))[0];
                    else
                        mm = (await message.MentionedChannels.First().DownloadMessages(1, id, Relative.Around, false))[0];

                    string response = $"*On {mm.Timestamp.ToShortDateString()} at {mm.Timestamp.ToShortTimeString()} UTC @{mm.User.Name} said:*\n{mm.RawText}";

                    message.Reply(response.Length > 2000 ? response.Substring(0, 2000) : response);
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Looks up a Discord message by its ID number", "<messageid> [channel mention]"));
            Commands.Add(new Command("!back", new Action<Message>((Message message) =>
            {
                try
                {
                    Random rand = new Random();
                    string[] args = GetArgs(message.Text);
                    if (args.Length < 1 || args[0].Length > 25) throw new ParameterException("Please provide a string from which to make a backronym that is 25 or fewer characters.");

                    int count = 1;
                    if (args.Length > 1) int.TryParse(args[1], out count);
                    count = count < 1 ? 1 : count > 10 ? 10 : count;

                    string backronym = "";
                    bool useComplex = false;
                    for (int i = 0; i < count; i++)
                    {
                        backronym += (args[0].StartsWith("&") ? args[0].Skip(1).ToString().ToUpper() : args[0].ToUpper()) + ": ";
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
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }), "Creates an backronym for the given letters", "<letters (start with ampersand for extended dictionary)> [count (max 10)]"));


            Commands = Commands.OrderBy(c => c.Text).ToList();
        }

        private static void LogError(Message message, Exception ex)
        {
            if (!(ex is ParameterException))
            {
                LogEvent(ex.ToString(), EventType.Error);
                message.Reply($"*An error occurred: {ex.Message}*");
            }
            else message.Reply($"*Command failed: {ex.Message}*", 20000);
        }

        private static Dictionary<string, string> LoadAliases(Server server)
        {
            if (File.Exists($"{BasePath}aliases.{server.Id}.txt"))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText($"{BasePath}aliases.{server.Id}.txt"));
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
                    if (message.Text.StartsWithAny(c.Text.Split('|')))
                    {
                        if (message.Channel.Server != null)
                            LogEvent($"{message.Channel.Server.Name.Shorten(15)}#{message.Channel.Name} - @{message.User.Name}#{message.User.Discriminator.ToString("D4")}: {message.Text}", EventType.MessageReceived);
                        if (message.User.IsBot)
                        {
                            LogEvent($"Ignored {c.Text} command from BOT user {message.User.Name}");
                            return;
                        }
                        else if (c.CommandContext == Command.Context.OwnerOnly && message.User.Id == 85877191371427840)
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
    }

    public static class ExtMethods
    {
        public static async void Reply(this Message message, string text, int deleteAfter = 0)
        {
            if (deleteAfter != 0)
                (await message.Channel.SendMessage(text)).DeleteAfterDelay(deleteAfter);
            else
                await message.Channel.SendMessage(text);
        }

        public static bool StartsWithAny(this string s, string[] values)
        {
            foreach (string v in values)
                if (s.StartsWith(v)) return true;
            return false;
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
