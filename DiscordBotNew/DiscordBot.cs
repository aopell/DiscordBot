using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew
{
    public class DiscordBot
    {
        public static DiscordSocketClient Client;

        public static void Main(string[] args)
            => new DiscordBot().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            Client = new DiscordSocketClient();

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;

            if (!SettingsManager.GetSetting("token", out string token)) throw new KeyNotFoundException("Token not found in settings file");
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            string commandPrefix = CommandTools.GetCommandPrefix(arg.Channel);

            if (arg.Content.Trim().StartsWith(commandPrefix))
            {
                await CommandRunner.Run(arg, commandPrefix);
            }
        }

        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
