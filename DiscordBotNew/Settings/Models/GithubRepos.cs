using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.Settings.Models
{
    [ConfigFile("github.json")]
    public class GithubRepos : Config
    {
        public string Username { get; set; }
        public string Token { get; set; }
        public Dictionary<ulong, string> Repositories { get; set; }

        public GithubRepos()
        {
            Repositories = new Dictionary<ulong, string>();
        }
    }
}
