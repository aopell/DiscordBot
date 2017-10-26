using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace DiscordBotNew.Commands
{
    public class GrammarPolice
    {
        private DiscordBot parent;
        public DiscordSocketClient Client { get; private set; }
        public DiscordRestClient RestClient { get; private set; }
        public List<string> FileNames { get; private set; }

        private readonly List<ulong> updatingChannels = new List<ulong>();
        private List<(ulong senderId, ulong receiverId, DateTimeOffset timestamp, string message)> reminders;

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
            var client = new HttpClient();
            try
            {
                var response = await client.PostAsync("https://languagetool.org/api/v2/check", new StringContent($"text={System.Web.HttpUtility.UrlEncode(arg.Content)}&language=en-US", Encoding.UTF8, "application/x-www-form-urlencoded"));
                string content = await response.Content.ReadAsStringAsync();
                JObject result = JObject.Parse(content);
                var matches = result["matches"];
                StringBuilder message = new StringBuilder();
                foreach (var match in matches)
                {
                    message.AppendLine($"{match["message"].Value<string>()}: {match["rule"]["description"].Value<string>()}");
                }
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
