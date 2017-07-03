using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;

namespace DiscordBotNew.Commands
{
    public static class BotCommands
    {
        [Command("hello", "test"), HelpText("Says hi")]
        public static async Task Hello(SocketMessage message)
        {
            await message.Reply("Hello there! :hand_splayed:");
        }

        [Command("multiply", "mult"), HelpText("Multiplies two numbers")]
        public static async Task Multiply(SocketMessage message, int num1, int num2)
        {
            await message.Reply($"{num1} * {num2} = {num1 * num2}");
        }

        [Command("echo", "say"), HelpText("Repeats the provided text back to you")]
        public static async Task Echo(SocketMessage message, [JoinRemainingParameters] string text)
        {
            await message.Reply(text);
        }
    }
}
