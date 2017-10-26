using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace DiscordBotNew.Commands
{
    public class GrammarPolice
    {
        private DiscordBot parent;
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }

        public GrammarPolice(DiscordBot parent)
        {
            this.parent = parent;
            Client = new DiscordSocketClient();
            RestClient = new DiscordRestClient();

            Client.Log += Log;
            Client.MessageReceived += Client_MessageReceived;
        }

        public async Task Start()
        {
            if (!parent.Settings.GetSetting("grammarToken", out string token)) throw new KeyNotFoundException("Token not found in settings file");
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            await RestClient.LoginAsync(TokenType.Bot, token);
        }

        public async Task Stop()
        {
            await Client.StopAsync();
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;

            var client = new HttpClient();
            try
            {
                string[] bypassMistakes =
                {
                    "UPPERCASE_SENTENCE_START"
                };

                var response = await client.PostAsync("https://languagetool.org/api/v2/check", new StringContent($"text={System.Web.HttpUtility.UrlEncode(arg.Content)}&language=en-US", Encoding.UTF8, "application/x-www-form-urlencoded"));
                string content = await response.Content.ReadAsStringAsync();
                JObject result = JObject.Parse(content);
                var matches = result["matches"];
                var words = arg.Content.Split(' ');
                StringBuilder message = new StringBuilder();
                foreach (var match in matches)
                {
                    if (bypassMistakes.Contains(match["rule"]["id"].Value<string>())) continue;
                    message.AppendLine($"{match["message"].Value<string>()}: `{words[match["offset"].Value<int>()]}`");
                }
                if (message.Length == 0) return;
                await arg.Channel.SendMessageAsync(message.ToString());
            }
            catch
            {

            }
        }

        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
