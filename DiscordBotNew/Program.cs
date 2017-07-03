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
    class Program
    {
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            CommandRunner.LoadCommands();
            var client = new DiscordSocketClient();

            client.Log += Log;
            client.MessageReceived += Client_MessageReceived;

            if (!SettingsManager.GetSetting("token", out string token)) throw new KeyNotFoundException("Token not found in settings file");
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            SettingsManager.GetSetting("commandPrefix", out string commandPrefix);
            commandPrefix = commandPrefix ?? "!";

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
