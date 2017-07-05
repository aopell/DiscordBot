using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public DiscordSocketClient Client { get; private set; }
        public SettingsManager Settings { get; private set; }
        public SettingsManager ChannelDescriptions { get; private set; }

        public static void Main(string[] args) => new DiscordBot().MainAsync().GetAwaiter().GetResult();

        private bool updatingDescriptions = false;

        public async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            CreateFiles();
            Settings = new SettingsManager(SettingsManager.BasePath + "settings.json");
            ChannelDescriptions = new SettingsManager(SettingsManager.BasePath + "descriptions.json");
            Client = new DiscordSocketClient();

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;
            Client.Ready += Client_Ready;
            Client.ChannelUpdated += Client_ChannelUpdated;

            if (!Settings.GetSetting("token", out string token)) throw new KeyNotFoundException("Token not found in settings file");
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private void CreateFiles()
        {
            CreateFile("settings.json");
            CreateFile("descriptions.json");

            void CreateFile(string filename)
            {
                if (!File.Exists(SettingsManager.BasePath + filename))
                    File.Create(SettingsManager.BasePath + filename).Close();
            }
        }

        private async Task Client_ChannelUpdated(SocketChannel arg1, SocketChannel arg2)
        {
            await Log(new LogMessage(LogSeverity.Info, "Ch Update", $"Channel updated: {((IChannel)arg2).Name}"));

            if (!updatingDescriptions && arg2 is ITextChannel textChannel)
            {
                var channelDescriptions = ChannelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> descriptions)
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

                ChannelDescriptions.AddSetting("descriptions", channelDescriptions);
                ChannelDescriptions.SaveSettings();
            }

            updatingDescriptions = false;
        }

        private async void Timer()
        {
            Regex descriptionCommandRegex = new Regex("{{(.*?)}}");
            if (ChannelDescriptions.GetSetting("descriptions", out Dictionary<ulong, string> descriptions))
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
                                         return (await CommandRunner.Run(m.Groups[1].Value, context, CommandTools.GetCommandPrefix(context, channel as ISocketMessageChannel), true)).ToString();
                                     });
                    updatingDescriptions = true;
                    try
                    {
                        await channel.ModifyAsync(ch => ch.Topic = newDesc);
                    }
                    catch (Exception ex)
                    {
                        updatingDescriptions = false;
                    }
                }
            }
        }

        private async Task Client_Ready()
        {
            if (Settings.GetSetting("botOwner", out ulong id))
                await Client.GetUser(id).SendMessageAsync($"[{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Pacific Standard Time")}] Now online!");

            while (true)
            {
                Timer();
                await Task.Delay(10000);
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
