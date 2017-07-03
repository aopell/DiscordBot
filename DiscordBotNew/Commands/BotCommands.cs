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
        private static Random random = new Random();

        [Command("hello", "test"), HelpText("Says hi")]
        public static async void Hello(SocketMessage message)
        {
            await message.Reply("Hello there! :hand_splayed:");
        }

        [Command("echo", "say"), HelpText("Repeats the provided text back to you")]
        public static async void Echo(SocketMessage message, [JoinRemainingParameters] string text)
        {
            await message.Reply(text);
        }

        [Command("8ball"), HelpText("It knows your future")]
        public static async void Magic8Ball(SocketMessage message, [HelpText("yes or no question"), JoinRemainingParameters] string question)
        {
            string[] responses = {
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

            await message.Reply($"<@{message.Author.Id}>: ***{question}***\n" + responses[random.Next(responses.Length)]);
        }
    }
}
