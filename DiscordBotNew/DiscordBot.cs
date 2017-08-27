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

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }
        public SettingsManager Settings { get; private set; }

        private const string ExceptionFilePath = SettingsManager.BasePath + "exception.txt";
        private SettingsManager channelDescriptions;
        public SettingsManager UserStatuses { get; private set; }
        public SettingsManager Leaderboards { get; private set; }
        public SettingsManager DynamicMessages { get; private set; }
        public SettingsManager Countdowns { get; private set; }
        public List<string> FileNames { get; private set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            new DiscordBot().MainAsync().GetAwaiter().GetResult();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(ExceptionFilePath, e.ExceptionObject.ToString());
        }

        private List<ulong> updatingChannels = new List<ulong>();

        public async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            CreateFiles();
            Settings = new SettingsManager(SettingsManager.BasePath + "settings.json");
            channelDescriptions = new SettingsManager(SettingsManager.BasePath + "descriptions.json");
            UserStatuses = new SettingsManager(SettingsManager.BasePath + "statuses.json");
            Leaderboards = new SettingsManager(SettingsManager.BasePath + "leaderboards.json");
            DynamicMessages = new SettingsManager(SettingsManager.BasePath + "dynamic-messages.json");
            Countdowns = new SettingsManager(SettingsManager.BasePath + "countdowns.json");
            Client = new DiscordSocketClient();
            RestClient = new DiscordRestClient();

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;
            Client.Ready += Client_Ready;
            Client.ChannelUpdated += Client_ChannelUpdated;
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
                Dictionary<ulong, UserStatusInfo> statuses;
                statuses = UserStatuses.GetSetting("statuses", out statuses) ? statuses : new Dictionary<ulong, UserStatusInfo>();
                DateTimeOffset currentTime = DateTimeOffset.Now;

                if (statuses.ContainsKey(arg2.Id))
                {
                    var previousStatus = statuses[arg2.Id];

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
                        StartedPlaying = null
                    };
                    statuses.Add(arg2.Id, status);
                }

                UserStatuses.AddSetting("statuses", statuses);
                UserStatuses.SaveSettings();
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

            void createFile(string filename)
            {
                FileNames.Add(filename);
                if (!File.Exists(SettingsManager.BasePath + filename))
                    File.Create(SettingsManager.BasePath + filename).Close();
            }
        }

        private async Task Client_ChannelUpdated(SocketChannel arg1, SocketChannel arg2)
        {
            await Log(new LogMessage(LogSeverity.Info, "Ch Update", $"Channel updated: {(arg2 as IGuildChannel)?.Guild.Name ?? ""}#{((IChannel)arg2).Name}"));

            if (!updatingChannels.Contains(arg2.Id) && arg2 is ITextChannel textChannel && textChannel.Topic != ((ITextChannel)arg1).Topic)
            {
                var channelDescriptions = this.channelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> descriptions)
                                              ? descriptions
                                              : new Dictionary<ulong, string>();
                Regex descriptionCommandRegex = new Regex("{{(.*?)}}");
                if (descriptionCommandRegex.IsMatch(textChannel.Topic))
                {
                    if (channelDescriptions.ContainsKey(textChannel.Id))
                    {
                        channelDescriptions[textChannel.Id] = textChannel.Topic;
                    }
                    else
                    {
                        channelDescriptions.Add(textChannel.Id, textChannel.Topic);
                    }
                }
                else
                {
                    if (channelDescriptions.ContainsKey(textChannel.Id))
                    {
                        channelDescriptions.Remove(textChannel.Id);
                    }
                }

                this.channelDescriptions.AddSetting("descriptions", channelDescriptions);
                this.channelDescriptions.SaveSettings();
            }

            updatingChannels.Remove(arg2.Id);
        }

        private async void SecondTimer(ulong tick)
        {
#if !DEBUG
            bool abort = false;
            Regex descriptionCommandRegex = new Regex("{{(.*?)}}");
            if (channelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> descriptions))
            {
                foreach (var item in descriptions)
                {
                    var channel = (ITextChannel)Client.GetChannel(item.Key);
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

                    updatingChannels.Add(channel.Id);
                    try
                    {
                        await channel.ModifyAsync(ch => ch.Topic = newDesc);
                    }
                    catch (Exception ex)
                    {
                        updatingChannels.Remove(channel.Id);
                    }
                }
            }
#endif
        }

        private async void MinuteTimer(ulong minute)
        {
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
                if (Settings.GetSetting("botOwner", out ulong id))
                {
#if !DEBUG
                await Client.GetUser(id).SendMessageAsync($"[{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time")}] Now online!");
#else
                    await Client.GetUser(id).SendMessageAsync($"[DEBUG] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time")}] Now online!");
#endif

                    if (File.Exists(ExceptionFilePath))
                    {
                        string message = "ERROR:\n\n" + File.ReadAllText(ExceptionFilePath);
                        foreach (string m in Enumerable.Range(0, message.Length / 1500 + 1).Select(i => message.Substring(i * 1500, message.Length - i * 1500 > 1500 ? 1500 : message.Length - i * 1500)))
                        {
                            await Client.GetUser(id).SendMessageAsync(m);
                        }
                        File.Delete(ExceptionFilePath);
                    }
                }

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
                    await Client.GetUser(id).SendMessageAsync($"[DEBUG] [{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time")}] {ex}");
            }
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var context = new DiscordUserMessageContext((IUserMessage)arg, this);
            string commandPrefix = CommandTools.GetCommandPrefix(context, context.Channel);

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
    }
}
