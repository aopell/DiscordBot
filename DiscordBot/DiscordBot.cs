using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using System.IO;
using Newtonsoft.Json;

namespace DiscordBot
{
    public static class DiscordBot
    {
        public static DiscordClient Client;

        public static void ConnectClient()
        {
            Client = new DiscordClient();

            Client.ExecuteAndWait(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Client.Connect(Config.Token, TokenType.Bot);
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
            if (e.Message.MentionedUsers.Any(u => u.Id == 168158669224017922))
                LogEvent(e.Message.Text);

            if (e.Message.IsAuthor)
            {
                LogEvent(e.Channel.Server != null ? $"Sent \"{e.Message.Text}\" to {e.Channel.Server.Name.Shorten(15)}#{e.Channel.Name}" : $"DM To @{e.Channel.Users.First(x => x.Id != Client.CurrentUser.Id).Name}#{e.User.Discriminator:D4}: \"{e.Message.Text}\"", EventType.BotAction);
            }
            else if (e.Channel.Server == null)
            {
                LogEvent($"DM From @{e.User.Name}#{e.User.Discriminator:D4}: {e.Message.Text}");
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

            await (await Client.CreatePrivateChannel(Config.OwnerId)).SendMessage($"{DateTime.Now:[yyyy-MM-dd HH:mm:ss]} Now online!");

            BotCommands.Load();
        }

        private static void Client_MessageUpdated(object sender, MessageUpdatedEventArgs e)
        {
            //LogEvent($"{e.Channel.Server.Name.Shorten(15)}#{e.Channel.Name} - @{e.User.Name}#{e.User.Discriminator.ToString("D4")}: Message updated from \"{e.Before.Text}\" to \"{e.After.Text}\"", EventType.MessageUpdated);
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
                string message = $"@{e.Before.Name}#{e.Before.Discriminator:D4} is now @{e.After.Name}#{e.After.Discriminator:D4}";
                if (lastMessage != (lastMessage = message)) LogEvent(message, EventType.UsernameUpdated);
            }

            if (e.Before.Status != e.After.Status)
            {
                string message = $"@{e.After.Name}#{e.After.Discriminator:D4} is now {e.After.Status}";
                if (lastMessage != (lastMessage = message))
                {
                    LogEvent(message, EventType.StatusUpdated);

                    if (e.Before.Status != UserStatus.Online && e.After.Status == UserStatus.Online)
                    {
                        var reminders = JsonConvert.DeserializeObject<Dictionary<ulong, List<string>>>(File.ReadAllText(Config.RemindersPath));
                        if (reminders.ContainsKey(e.After.Id))
                        {
                            foreach (string s in reminders[e.After.Id])
                            {
                                e.After.SendMessage(s);
                            }

                            reminders.Remove(e.After.Id);
                            File.WriteAllText(Config.RemindersPath, JsonConvert.SerializeObject(reminders));
                        }
                    }
                }
            }

            if (e.Before.CurrentGame.GetValueOrDefault(new Game("")).Name != e.After.CurrentGame.GetValueOrDefault(new Game("")).Name)
            {
                string message = "";
                string tempMessage;
                if (e.After.CurrentGame.HasValue && !e.Before.CurrentGame.HasValue)
                {
                    tempMessage = $"game.start.{e.After.Id}.{e.After.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    message = $"@{e.After.Name}#{e.After.Discriminator:D4} is now playing {e.After.CurrentGame.Value.Name}";
                    Gamer.GameStarted(e.After);
                }
                else if (e.Before.CurrentGame.HasValue && !e.After.CurrentGame.HasValue)
                {
                    tempMessage = $"game.stop.{e.After.Id}.{e.Before.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    var time = Gamer.GameStopped(e.After, e.Before.CurrentGame.Value.Name);
                    message = $"@{e.After.Name}#{e.After.Discriminator:D4} is no longer playing {e.Before.CurrentGame.Value.Name} after {Math.Round(time.TotalHours, 2)} hours";
                }
                else if (e.Before.CurrentGame.HasValue && e.After.CurrentGame.HasValue)
                {
                    tempMessage = $"game.switch.{e.After.Id}.{e.Before.CurrentGame.Value.Name}.{e.After.CurrentGame.Value.Name}";
                    if (lastMessage == (lastMessage = tempMessage)) return;
                    var time = Gamer.GameStopped(e.After, e.Before.CurrentGame.Value.Name);
                    message = $"@{e.After.Name}#{e.After.Discriminator:D4} switched from playing {e.Before.CurrentGame.Value.Name} to {e.After.CurrentGame.Value.Name}  after {Math.Round(time.TotalHours, 2)} hours";
                    Gamer.GameStarted(e.After);
                }

                LogEvent(message, EventType.GameUpdated);
            }

            if (e.Before.Nickname != e.After.Nickname)
            {
                string message = !string.IsNullOrEmpty(e.After.Nickname) ? $"@{e.After.Name}#{e.After.Discriminator:D4} is now known as {e.After.Nickname} on {e.Server.Name}" : $"@{e.After.Name}#{e.After.Discriminator:D4} no longer has a nickname on {e.Server.Name}";

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
            string logMessage = $"{DateTime.Now:[HH:mm:ss]} [{type}] {message}";
            Console.WriteLine(logMessage);
            Console.ForegroundColor = ConsoleColor.Gray;

            try
            {
                if (!Directory.Exists(Config.LogDirectoryPath))
                    Directory.CreateDirectory(Config.LogDirectoryPath);
                File.AppendAllLines($"{Config.LogDirectoryPath}\\{DateTime.Now:yyyy-MM-dd}.log", new[] { logMessage });
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

        public static void LogError(Message message, Exception ex)
        {
            if (!(ex is BotCommandException) && !(ex is CommandSyntaxException))
            {
                LogEvent(ex.ToString(), EventType.Error);
                message.Reply($"```diff\n- ERROR: {ex.Message}\n```");
            }
            else message.Reply($"```diff\n- Command failed: {ex.Message}\n```");
        }

        public static void LogError(Message message, string text)
        {
            message.Reply($"```diff\n- ERROR: {text}\n```");
        }

        private static async void RunCommandAction(Command c, Message message)
        {
            try
            {
                await message.Channel.SendIsTyping();
                var args = message.RawText.Split(' ').Skip(1).ToList();
                if (args.Count >= c.RequiredParameters)
                    c.Action(message, args);
                else throw new CommandSyntaxException(c.Names[0]);
            }
            catch (Exception ex)
            {
                LogError(message, ex);
            }
        }

        public static void CheckCommands(Message message)
        {
            foreach (Command c in BotCommands.Commands)
            {
                try
                {
                    if (message.Text.StartsWithAnyStrict(c.Names))
                    {
                        if (message.Channel.Server != null && "!anon".StartsWithAny(c.Names))
                            LogEvent($"{message.Channel.Server.Name.Shorten(15)}#{message.Channel.Name} - @{message.User.Name}#{message.User.Discriminator:D4}: {message.Text}");
                        if (message.User.IsBot)
                        {
                            LogEvent($"Ignored {c.NamesString} command from BOT user {message.User.Name}");
                            return;
                        }
                        else if (c.CommandContext == Command.Context.OwnerOnly && message.User.Id == Config.OwnerId)
                        {
                            RunCommandAction(c, message);
                        }
                        else if (c.CommandContext == Command.Context.DeletePermission && message.User.GetPermissions(message.Channel).ManageMessages)
                        {
                            RunCommandAction(c, message);
                        }
                        else if (c.CommandContext == Command.Context.All || c.CommandContext == GetMessageContext(message))
                        {
                            RunCommandAction(c, message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError(message, ex);
                }
            }
        }

        public static Command.Context GetMessageContext(Message message) => message.Server == null ? Command.Context.DirectMessage : Command.Context.GuildChannel;

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
        public static IEnumerable<string> SplitByLength(this string str, int maxLength)
        {
            for (int index = 0; index < str.Length; index += maxLength)
            {
                yield return str.Substring(index, Math.Min(maxLength, str.Length - index));
            }
        }

        public static Action<Message, List<string>> HandleErrors(this Action<Message, List<string>> action)
        {

            return (message, parameters) =>
            {
                try
                {
                    action(message, parameters);
                }
                catch (Exception ex)
                {
                    DiscordBot.LogError(message, ex);
                }
            };
        }

        public static async void Reply(this Message message, string text, int deleteAfter = 0)
        {
            foreach (string s in text.SplitByLength(2000))
            {

                if (deleteAfter != 0)
                {
                    (await message.Channel.SendMessage(s)).DeleteAfterDelay(deleteAfter);
                }
                else
                {
                    await message.Channel.SendMessage(s);
                }
            }
        }

        public static async void Reply(this Channel channel, string text, int deleteAfter = 0)
        {
            foreach (string s in text.SplitByLength(2000))
            {

                if (deleteAfter != 0)
                {
                    (await channel.SendMessage(s)).DeleteAfterDelay(deleteAfter);
                }
                else
                {
                    await channel.SendMessage(s);
                }
            }
        }

        public static bool StartsWithAny(this string s, string[] values)
        {
            foreach (string v in values)
                if (s == v || s.StartsWith(v)) return true;
            return false;
        }

        public static bool StartsWithAnyStrict(this string s, string[] values)
        {
            foreach (string v in values)
                if (s == v || s.StartsWith(v + " ")) return true;
            return false;
        }

        public static string Shorten(this string s, int maxLength, bool elipses = true)
        {
            if (s.Length > maxLength)
            {
                return elipses ? $"{s.Substring(0, maxLength - 3).TrimEnd()}..." : s.Substring(0, maxLength).TrimEnd();
            }
            return s;
        }

        public static async void DeleteAfterDelay(this Message message, int delay)
        {
            await Task.Delay(delay).ContinueWith(x => message.Delete());
            DiscordBot.LogEvent(message.Channel.Server != null ? $"{message.Channel.Server.Name.Shorten(15)}#{message.Channel.Name} - @{message.User.Name}#{message.User.Discriminator:D4}: Message Deleted" : $"@{message.User.Name}#{message.User.Discriminator:D4}: DM Deleted", DiscordBot.EventType.BotAction);
        }
    }
}
