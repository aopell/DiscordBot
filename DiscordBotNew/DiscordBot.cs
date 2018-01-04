using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.Commands;
using DiscordBotNew.Settings;
using DiscordBotNew.Settings.Models;
using Newtonsoft.Json;

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }
        public SettingsManager Settings { get; private set; }

        private const string ExceptionFilePath = SettingsManager.BasePath + "exception.txt";
        public SettingsManager ChannelDescriptions { get; set; }
        private SettingsManager StatusSettings { get; set; }
        public SettingsManager Leaderboards { get; private set; }
        public SettingsManager DynamicMessages { get; private set; }
        public SettingsManager Countdowns { get; private set; }
        public GrammarPolice Grammar { get; private set; }
        public List<string> FileNames { get; private set; }
        public Dictionary<ulong, UserStatusInfo> CurrentUserStatuses { get; private set; }

        private SettingsManager remindersManager;
        private List<(ulong senderId, ulong receiverId, DateTimeOffset timestamp, string message)> reminders;

        public string DefaultTimeZone { get; private set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            new DiscordBot().MainAsync().GetAwaiter().GetResult();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(ExceptionFilePath, e.ExceptionObject.ToString());
        }

        private async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            CreateFiles();

            //BotSettings settings = Config.LoadConfig<BotSettings>("settings.json");
            //ChannelDescriptions descriptions = Config.LoadConfig<ChannelDescriptions>("descriptions.json");
            //UserStatuses s = Config.LoadConfig<UserStatuses>("statuses.json");

            Settings = new SettingsManager(SettingsManager.BasePath + "settings.json");
            ChannelDescriptions = new SettingsManager(SettingsManager.BasePath + "descriptions.json");
            StatusSettings = new SettingsManager(SettingsManager.BasePath + "statuses.json");
            Leaderboards = new SettingsManager(SettingsManager.BasePath + "leaderboards.json");
            DynamicMessages = new SettingsManager(SettingsManager.BasePath + "dynamic-messages.json");
            Countdowns = new SettingsManager(SettingsManager.BasePath + "countdowns.json");
            remindersManager = new SettingsManager(SettingsManager.BasePath + "reminders.json");
            StatusSettings.GetSetting("statuses", out Dictionary<ulong, UserStatusInfo> statuses);
            CurrentUserStatuses = statuses ?? new Dictionary<ulong, UserStatusInfo>();
            Client = new DiscordSocketClient();
            RestClient = new DiscordRestClient();
            Grammar = new GrammarPolice(this);
            DefaultTimeZone = Settings.GetSetting("timezone", out string tz) ? tz : "UTC";

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;
            Client.Ready += Client_Ready;
            Client.GuildMemberUpdated += Client_GuildMemberUpdated;

            if (!Settings.GetSetting("token", out string token)) throw new KeyNotFoundException("Token not found in settings file");
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            await RestClient.LoginAsync(TokenType.Bot, token);

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            await Log(new LogMessage(LogSeverity.Info, "UserUpdate", $"{arg2.Username} updated"));

            if (arg1.Status != arg2.Status || arg1.Game?.Name != arg2.Game?.Name)
            {
                DateTimeOffset currentTime = DateTimeOffset.Now;

                if (CurrentUserStatuses.ContainsKey(arg2.Id))
                {
                    var previousStatus = CurrentUserStatuses[arg2.Id];

                    if (arg1.Status != arg2.Status)
                    {
                        previousStatus.StatusLastChanged = currentTime;
                        if (arg1.Status == UserStatus.Online)
                        {
                            previousStatus.LastOnline = currentTime;
                        }
                    }
                    if (arg1.Game?.Name != arg2.Game?.Name)
                    {
                        previousStatus.Game = arg2.Game?.Name;
                        previousStatus.StartedPlaying = DateTimeOffset.Now;
                    }
                }
                else
                {
                    UserStatusInfo status = new UserStatusInfo
                    {
                        StatusLastChanged = currentTime,
                        LastOnline = arg1.Status == UserStatus.Online ? currentTime : DateTimeOffset.MinValue,
                        Game = null,
                        StartedPlaying = null,
                        LastMessageSent = DateTimeOffset.MinValue
                    };
                    CurrentUserStatuses.Add(arg2.Id, status);
                }
            }
        }

        private void CreateFiles()
        {
            FileNames = new List<string>();
            createFile("settings.json");
            createFile("descriptions.json");
            createFile("statuses.json");
            createFile("leaderboards.json");
            createFile("dynamic-messages.json");
            createFile("countdowns.json");
            createFile("reminders.json");

            void createFile(string filename)
            {
                FileNames.Add(filename);
                if (!File.Exists(SettingsManager.BasePath + filename))
                    File.Create(SettingsManager.BasePath + filename).Close();
            }
        }

        private async void SecondTimer(ulong tick)
        {
            bool abort = false;
            Regex descriptionCommandRegex = new Regex("{{(.*?)}}");
            if (ChannelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> descriptions))
            {
                foreach (var item in descriptions)
                {
                    var channel = (ITextChannel)Client.GetChannel(item.Key);
                    if (channel == null)
                    {
                        var channelDescriptions = ChannelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> d)
                            ? descriptions
                            : new Dictionary<ulong, string>();
                        d.Remove(item.Key);
                        ChannelDescriptions.AddSetting("descriptions", d);
                        ChannelDescriptions.SaveSettings();
                        continue;
                    }
                    string topic = item.Value;
                    string newDesc = await descriptionCommandRegex.ReplaceAsync(
                                     topic,
                                     async m =>
                                     {
                                         var context = new DiscordChannelDescriptionContext(m.Groups[1].Value, channel, this);
                                         var result = await CommandRunner.RunTimer(m.Groups[1].Value, context, CommandTools.GetCommandPrefix(context, channel as IMessageChannel), true, tick);
                                         abort = result == null;
                                         return result?.ToString().Trim() ?? "";
                                     });
                    if (abort)
                    {
                        continue;
                    }

                    try
                    {
                        await channel.ModifyAsync(ch => ch.Topic = newDesc);
                    }
                    catch
                    {
                        // Fail silently
                    }
                }
            }
        }

        private async void MinuteTimer(ulong minute)
        {
            StatusSettings.AddSetting("statuses", CurrentUserStatuses);
            StatusSettings.SaveSettings();

            foreach (var reminder in reminders.Where(reminder => reminder.timestamp < DateTimeOffset.Now))
            {
                EmbedBuilder embed = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        Name = $"{Client.GetUser(reminder.senderId)?.Username ?? "Someone"} sent you a reminder",
                        IconUrl = Client.GetUser(reminder.senderId)?.AvatarUrlOrDefaultAvatar()
                    },
                    Description = reminder.message,
                    Timestamp = reminder.timestamp,
                    ThumbnailUrl = "http://icons.iconarchive.com/icons/webalys/kameleon.pics/512/Bell-icon.png",
                    Color = new Color(224, 79, 95)
                };

                await Client.GetUser(reminder.receiverId).SendMessageAsync("", embed: embed);
            }

            reminders.RemoveAll(x => x.timestamp < DateTimeOffset.Now);
            remindersManager.AddSetting("reminders", reminders);
            remindersManager.SaveSettings();

            if (DynamicMessages.GetSetting("messages", out List<DynamicMessage> messages))
            {
                List<DynamicMessage> toRemove = new List<DynamicMessage>();
                foreach (var message in messages)
                {
                    try
                    {
                        if (minute % message.UpdateInterval != 0)
                        {
                            continue;
                        }

                        var channel = (IMessageChannel)Client.GetGuild(message.GuildId).GetChannel(message.ChannelId);
                        var discordMessage = (IUserMessage)await channel.GetMessageAsync(message.MessageId);

                        if (discordMessage == null)
                        {
                            toRemove.Add(message);
                            continue;
                        }

                        var context = new DiscordDynamicMessageContext(discordMessage, this, message.CommandText);
                        string prefix = CommandTools.GetCommandPrefix(context, channel);
                        if (message.CommandText.StartsWith(prefix))
                        {
                            await CommandRunner.Run(message.CommandText, context, prefix, false);
                        }
                        else
                        {
                            await context.ReplyError($"The string `{message.CommandText}`", "Invalid Command");
                        }
                    }
                    catch
                    {
                        // Fail silently
                    }
                }

                if (toRemove.Count > 0)
                {
                    DynamicMessages.AddSetting("messages", messages.Except(toRemove));
                    DynamicMessages.SaveSettings();
                }
            }
        }

        private async Task Client_Ready()
        {
            try
            {
                DateTimeOffset currentTime = DateTimeOffset.Now;
                UserStatusInfo status = new UserStatusInfo
                {
                    StatusLastChanged = currentTime,
                    LastOnline = currentTime,
                    Game = null,
                    StartedPlaying = null
                };
                CurrentUserStatuses.Remove(Client.CurrentUser.Id);
                CurrentUserStatuses.Add(Client.CurrentUser.Id, status);

                if (Settings.GetSetting("botOwner", out ulong id))
                {
                    if (Settings.GetSetting("announceStartup", out bool announce) && announce)
                    {
#if !DEBUG
                        await Client.GetUser(id).SendMessageAsync($"[{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] Now online!");
#else
                        await Client.GetUser(id).SendMessageAsync($"[DEBUG] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] Now online!");
#endif
                    }

                    if (File.Exists(ExceptionFilePath))
                    {
                        string message = "***The bot has restarted due to an error***:\n\n" + File.ReadAllText(ExceptionFilePath);
                        foreach (string m in Enumerable.Range(0, message.Length / 1500 + 1).Select(i => message.Substring(i * 1500, message.Length - i * 1500 > 1500 ? 1500 : message.Length - i * 1500)))
                        {
                            await Client.GetUser(id).SendMessageAsync(m);
                        }
                        File.Delete(ExceptionFilePath);
                    }
                }

                if (Settings.GetSetting("game", out string game))
                {
                    await Client.SetGameAsync(game);
                }

                reminders = remindersManager.GetSetting("reminders", out List<(ulong senderId, ulong receiverId, DateTimeOffset time, string message)> loadedReminders) ? loadedReminders : new List<(ulong senderId, ulong receiverId, DateTimeOffset timestamp, string message)>();

                ulong tick = 0;
                while (true)
                {
                    if (tick % 60 == 0)
                        MinuteTimer(tick / 60);
                    SecondTimer(tick++);
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                if (Settings.GetSetting("botOwner", out ulong id))
                    await Client.GetUser(id).SendMessageAsync($"[ERROR] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] {ex}");
            }
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var context = new DiscordUserMessageContext((IUserMessage)arg, this);
            string commandPrefix = CommandTools.GetCommandPrefix(context, context.Channel);
            var status = CurrentUserStatuses.GetValueOrDefault(arg.Author.Id) ??
                         new UserStatusInfo
                         {
                             StatusLastChanged = DateTimeOffset.MinValue,
                             LastOnline = DateTimeOffset.MinValue,
                             Game = null,
                             StartedPlaying = null,
                             LastMessageSent = DateTimeOffset.MinValue
                         };
            status.LastMessageSent = DateTimeOffset.Now;
            CurrentUserStatuses[arg.Author.Id] = status;
            if (arg.Content.Trim().StartsWith(commandPrefix) && !arg.Author.IsBot)
            {
                await CommandRunner.Run(arg.Content, context, commandPrefix, false);
            }
        }

        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public void AddReminder((ulong, ulong, DateTimeOffset, string) reminder)
        {
            reminders.Add(reminder);
            remindersManager.AddSetting("reminders", reminders);
            remindersManager.SaveSettings();
        }

        public void DeleteReminder((ulong, ulong, DateTimeOffset, string) reminder)
        {
            reminders.Remove(reminder);
            remindersManager.AddSetting("reminders", reminders);
            remindersManager.SaveSettings();
        }

        public IEnumerable<(ulong sender, ulong receiver, DateTimeOffset time, string message)> GetReminders(ulong receiver) => reminders.Where(x => x.receiverId == receiver);
    }
}
