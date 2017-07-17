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
using DiscordBotNew.Commands;
using DiscordBotNew.Settings;

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }
        public SettingsManager Settings { get; private set; }
        private SettingsManager channelDescriptions;
        public SettingsManager UserStatuses { get; private set; }
        public SettingsManager Leaderboards { get; private set; }

        public static void Main(string[] args) => new DiscordBot().MainAsync().GetAwaiter().GetResult();

        private List<ulong> updatingChannels = new List<ulong>();

        public async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            CreateFiles();
            Settings = new SettingsManager(SettingsManager.BasePath + "settings.json");
            channelDescriptions = new SettingsManager(SettingsManager.BasePath + "descriptions.json");
            UserStatuses = new SettingsManager(SettingsManager.BasePath + "statuses.json");
            Leaderboards = new SettingsManager(SettingsManager.BasePath + "leaderboards.json");
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

            if (arg1.Status != arg2.Status)
            {
                Dictionary<ulong, UserStatusInfo> statuses;
                statuses = UserStatuses.GetSetting("statuses", out statuses) ? statuses : new Dictionary<ulong, UserStatusInfo>();
                DateTimeOffset currentTime = DateTimeOffset.Now;

                if (statuses.ContainsKey(arg2.Id))
                {
                    var previousStatus = statuses[arg2.Id];
                    previousStatus.StatusLastChanged = currentTime;
                    if (arg1.Status == UserStatus.Online)
                    {
                        previousStatus.LastOnline = currentTime;
                    }
                }
                else
                {
                    UserStatusInfo status = new UserStatusInfo
                    {
                        StatusLastChanged = currentTime,
                        LastOnline = arg1.Status == UserStatus.Online ? currentTime : DateTimeOffset.MinValue
                    };
                    statuses.Add(arg2.Id, status);
                }

                UserStatuses.AddSetting("statuses", statuses);
                UserStatuses.SaveSettings();
            }
        }

        private void CreateFiles()
        {
            createFile("settings.json");
            createFile("descriptions.json");
            createFile("statuses.json");
            createFile("leaderboards.json");
            createFile("daily-leaderboards.json");

            void createFile(string filename)
            {
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

        private async void Timer(ulong tick)
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
                                         var result = await CommandRunner.RunTimer(m.Groups[1].Value, context, CommandTools.GetCommandPrefix(context, channel as ISocketMessageChannel), true, tick);
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

        private async Task Client_Ready()
        {
            if (Settings.GetSetting("botOwner", out ulong id))
                await Client.GetUser(id).SendMessageAsync($"[{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time")}] Now online!");

            ulong tick = 0;
            while (true)
            {
                Timer(tick++);
                await Task.Delay(1000);
            }
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var context = new DiscordMessageContext(arg, this);
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
