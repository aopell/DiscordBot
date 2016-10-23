using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public struct DiscordGame
    {
        public string Name;
        public GameType Type;
        public string Url;

        public DiscordGame(string name, GameType type, string url)
        {
            Name = name;
            Type = type;
            Url = url;
        }

        public DiscordGame(Game game)
        {
            Name = game.Name;
            Type = game.Type;
            Url = game.Url;
        }


        public static bool operator ==(DiscordGame a, DiscordGame b)
        {
            return a.Name == b.Name && a.Type == b.Type && a.Url == b.Url;
        }

        public static bool operator !=(DiscordGame a, DiscordGame b)
        {
            return !(a == b);
        }

        //public override string ToString()
        //{
        //    return JsonConvert.SerializeObject(this);
        //}
    }
}
