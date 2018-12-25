using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.Commands;
using DiscordBotNew.Settings;
using DiscordBotNew.Settings.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public static DiscordBot MainInstance = null;
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }
        public BotSettings Settings { get; private set; }
        public ChannelDescriptions ChannelDescriptions { get; private set; }
        public UserStatuses Statuses { get; private set; }
        public GuildLeaderboards Leaderboards { get; private set; }
        public DynamicMessages DynamicMessages { get; private set; }
        public GuildCountdowns Countdowns { get; private set; }
        public UserReminders Reminders { get; private set; }
        public EventsLog EventsLog { get; private set; }
        public GithubRepos GithubRepos { get; private set; }

        private const string ExceptionFilePath = Config.BasePath + "exception.txt";
        public GrammarPolice Grammar { get; private set; }
        public List<string> FileNames { get; private set; }

        private HashSet<ulong> currentlyEditing = new HashSet<ulong>();

        public string DefaultTimeZone { get; private set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            MainInstance = new DiscordBot();
            MainInstance.MainAsync().GetAwaiter().GetResult();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(MainInstance?.Settings.StatusMessageWebhook))
            {
                try
                {
                    new HttpClient().PostAsync(MainInstance.Settings.StatusMessageWebhook, new StringContent("{\"content\":\"[" + DateTimeOffset.Now + "] Process exiting\"}", Encoding.UTF8, "application/json"));
                }
                catch { }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!string.IsNullOrEmpty(MainInstance?.Settings.StatusMessageWebhook))
            {
                try
                {
                    new HttpClient().PostAsync(MainInstance.Settings.StatusMessageWebhook, new StringContent("{\"content\":\"[" + DateTimeOffset.Now + "] " + e.ExceptionObject + "\"}", Encoding.UTF8, "application/json"));
                }
                catch { }
            }

            File.WriteAllText(ExceptionFilePath, e.ExceptionObject.ToString());
        }

        private async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            CreateFiles();

            //BotSettings settings = Config.LoadConfig<BotSettings>("settings.json");
            //ChannelDescriptions descriptions = Config.LoadConfig<ChannelDescriptions>("descriptions.json");
            //UserStatuses s = Config.LoadConfig<UserStatuses>("statuses.json");

            Client = new DiscordSocketClient();
            RestClient = new DiscordRestClient();
            Grammar = new GrammarPolice(this);

            ConfigFileManager.LoadConfigFiles(this);

            DefaultTimeZone = Settings.Timezone ?? "UTC";

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;
            Client.Ready += Client_Ready;
            Client.GuildMemberUpdated += Client_GuildMemberUpdated;
            Client.ReactionAdded += Client_ReactionAdded;

            if (Settings.Token == null) throw new KeyNotFoundException("Token not found in settings file");
            await Client.LoginAsync(TokenType.Bot, Settings.Token);
            await Client.StartAsync();

            await RestClient.LoginAsync(TokenType.Bot, Settings.Token);

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId != Client.CurrentUser.Id)
            {
                var msg = await arg1.GetOrDownloadAsync();
                if (msg.Embeds.Count == 1 && !currentlyEditing.Contains(msg.Id) && PaginatedCommand.FooterRegex.IsMatch(msg.Embeds.First().Footer?.Text ?? ""))
                {
                    currentlyEditing.Add(msg.Id);
                    var context = new DiscordPaginatedMessageContext(arg3.Emote, msg, this);
                    string prefix = CommandTools.GetCommandPrefix(context, arg2);
                    await CommandRunner.Run($"{prefix}{context.Command} {context.UpdatedPageNumber}", context, prefix, true);
                    currentlyEditing.Remove(msg.Id);
                }
            }
        }

        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            await Log(new LogMessage(LogSeverity.Info, "UserUpdate", $"{arg2.Username} updated"));

            if (arg1.Status != arg2.Status || arg1.Activity?.Name != arg2.Activity?.Name)
            {
                DateTimeOffset currentTime = DateTimeOffset.Now;

                if (Statuses.Statuses.ContainsKey(arg2.Id))
                {
                    var previousStatus = Statuses.Statuses[arg2.Id];

                    if (arg1.Status != arg2.Status)
                    {
                        previousStatus.StatusLastChanged = currentTime;
                        if (arg1.Status == UserStatus.Online)
                        {
                            previousStatus.LastOnline = currentTime;
                        }
                    }
                    if (arg1.Activity?.Name != arg2.Activity?.Name)
                    {
                        previousStatus.Game = arg2.Activity?.Name;
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
                    Statuses.Statuses.Add(arg2.Id, status);
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
            createFile("log.json");
            createFile("github.json");

            void createFile(string filename)
            {
                FileNames.Add(filename);
                if (!File.Exists(Config.BasePath + filename))
                    File.Create(Config.BasePath + filename).Close();
            }
        }

        private async void SecondTimer(ulong tick)
        {
            bool abort = false;
            Regex descriptionCommandRegex = new Regex("{{(.*?)}}");
            if (ChannelDescriptions.Descriptions != null)
            {
                var toRemove = new List<ulong>();
                foreach (var item in ChannelDescriptions.Descriptions)
                {
                    var channel = (ITextChannel)Client.GetChannel(item.Key);
                    if (channel == null)
                    {
                        toRemove.Add(item.Key);
                        continue;
                    }
                    string topic = item.Value;
                    string newDesc = await descriptionCommandRegex.ReplaceAsync(
                                     topic,
                                     async m =>
                                     {
                                         var context = new DiscordChannelDescriptionContext(m.Groups[1].Value, channel, this);
                                         var result = await CommandRunner.RunTimer(m.Groups[1].Value, context, CommandTools.GetCommandPrefix(context, channel), true, tick);
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

                foreach (ulong d in toRemove)
                {
                    string status = $"Channel {d} with dynamic description '{ChannelDescriptions.Descriptions[d]}' deleted";
                    EventsLog.LogEvent(status);
                    await SendStatusMessage(status);
                    ChannelDescriptions.Descriptions.Remove(d);
                }
            }
        }

        private async void MinuteTimer(ulong minute)
        {
            Statuses.SaveConfig();

            bool countdownsModified = false;
            foreach (ulong guild in Countdowns.Countdowns.Keys)
            {
                ulong? channel = (Countdowns.CountdownChannels?.ContainsKey(guild) ?? false) ? Countdowns.CountdownChannels[guild] : (ulong?)null;
                if (channel == null) continue;
                foreach (var countdown in Countdowns.Countdowns[guild].ToList())
                {
                    if (countdown.Value < DateTimeOffset.Now)
                    {
                        await ((ITextChannel)Client.GetChannel(channel.Value)).SendMessageAsync("", embed: CommandTools.GenerateCountdownCompleteEmbed(this, countdown.Key, countdown.Value).Build());
                        countdownsModified = true;
                    }
                }
            }

            if (countdownsModified)
            {
                foreach (ulong guild in Countdowns.Countdowns.Keys)
                {
                    foreach (string key in Countdowns.Countdowns[guild].Where(x => x.Value < DateTimeOffset.Now).Select(x => x.Key).ToArray())
                    {
                        Countdowns.Countdowns[guild].Remove(key);
                    }
                }
                Countdowns.SaveConfig();
            }

            foreach (var reminder in Reminders.Reminders.Where(reminder => reminder.Timestamp < DateTimeOffset.Now))
            {
                EmbedBuilder embed = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        Name = $"{Client.GetUser(reminder.SenderId)?.Username ?? "Someone"} sent you a reminder",
                        IconUrl = Client.GetUser(reminder.SenderId)?.AvatarUrlOrDefaultAvatar()
                    },
                    Description = reminder.Message,
                    ThumbnailUrl = "http://icons.iconarchive.com/icons/webalys/kameleon.pics/512/Bell-icon.png",
                    Timestamp = reminder.Timestamp,
                    Color = new Color(224, 79, 95)
                };

                var message = await Client.GetUser(reminder.ReceiverId).SendMessageAsync("", embed: embed.Build());
                await message.AddReactionAsync(new Emoji("💤"));
            }

            Reminders.Reminders.RemoveAll(x => x.Timestamp < DateTimeOffset.Now);
            Reminders.SaveConfig();

            if (DynamicMessages.Messages != null)
            {
                var toRemove = new List<DynamicMessageInfo>();
                foreach (var message in DynamicMessages.Messages)
                {
                    try
                    {
                        if (minute % message.UpdateInterval != 0)
                        {
                            continue;
                        }

                        var channel = (IMessageChannel)Client.GetGuild(message.GuildId)?.GetChannel(message.ChannelId);
                        var discordMessage = (IUserMessage)await channel?.GetMessageAsync(message.MessageId);

                        if (discordMessage == null)
                        {
                            toRemove.Add(message);
                            string status = $"Dynamic message in channel {message.ChannelId} with ID {message.MessageId} deleted";
                            EventsLog.LogEvent(status);
                            await SendStatusMessage(status);
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

                DynamicMessages.Messages.RemoveAll(x => toRemove.Contains(x));
                DynamicMessages.SaveConfig();
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
                Statuses.Statuses.Remove(Client.CurrentUser.Id);
                Statuses.Statuses.Add(Client.CurrentUser.Id, status);

#if !DEBUG
                await SendStatusMessage($"[{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] Now online!");
#else
                await SendStatusMessage($"[DEBUG] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] Now online!");
#endif

                if (File.Exists(ExceptionFilePath))
                {
                    string message = "***The bot has restarted due to an error***:\n\n" + File.ReadAllText(ExceptionFilePath);
                    foreach (string m in Enumerable.Range(0, message.Length / 1500 + 1).Select(i => "```\n" + message.Substring(i * 1500, message.Length - i * 1500 > 1500 ? 1500 : message.Length - i * 1500) + "```"))
                    {
                        await SendStatusMessage(m, true);
                    }
                    File.Delete(ExceptionFilePath);
                }

                if (Settings.StartupReplyChannel.HasValue)
                {
                    try
                    {
                        await ((ITextChannel)Client.GetChannel(Settings.StartupReplyChannel.Value)).SendMessageAsync("I have returned");
                    }
                    catch
                    {
                        // Fail silently and set to null
                    }
                    Settings.StartupReplyChannel = null;
                    Settings.SaveConfig();
                }

                if (Settings.Game != null)
                {
                    await Client.SetGameAsync(Settings.Game);
                }

                Reminders.Reminders = Reminders.Reminders ?? new List<ReminderInfo>();

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
                await SendStatusMessage($"[ERROR] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, DefaultTimeZone)}] ```{ex}\n\n{ex.InnerException}```", true);
            }
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var context = new DiscordUserMessageContext((IUserMessage)arg, this);
            string commandPrefix = CommandTools.GetCommandPrefix(context, context.Channel);
            var status = Statuses.Statuses.GetValueOrDefault(arg.Author.Id) ??
                         new UserStatusInfo
                         {
                             StatusLastChanged = DateTimeOffset.MinValue,
                             LastOnline = DateTimeOffset.MinValue,
                             Game = null,
                             StartedPlaying = null,
                             LastMessageSent = DateTimeOffset.MinValue
                         };
            status.LastMessageSent = DateTimeOffset.Now;
            Statuses.Statuses[arg.Author.Id] = status;
            if (arg.Content.Trim().StartsWith(commandPrefix) && !arg.Author.IsBot)
            {
                await CommandRunner.Run(arg.Content, context, commandPrefix, false);
            }

            if (context.Guild != null 
                && GithubRepos.Username != null
                && GithubRepos.Token != null 
                && GithubRepos.Repositories.ContainsKey(context.Guild.Id)
                && GithubRepos.Repositories[context.Guild.Id] != null)
            {
                await GithubHelper.Run(context, GithubRepos.Username, GithubRepos.Token, GithubRepos.Repositories[context.Guild.Id]);
            }
        }

        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public async Task<bool> SendStatusMessage(string message, bool mentionOwner = false)
        {
            if (!Settings.StatusMessageChannel.HasValue) return false;

            try
            {
                await ((IMessageChannel)Client.GetChannel(Settings.StatusMessageChannel.Value)).SendMessageAsync((mentionOwner && Settings.OwnerId.HasValue ? $"<@{Settings.OwnerId}>\n" : "") + message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
