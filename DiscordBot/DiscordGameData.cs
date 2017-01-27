using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class DiscordGameData
    {
        public string Game;
        public TimeSpan TimePlayed;

        public DiscordGameData(string game, TimeSpan timePlayed)
        {
            Game = game;
            TimePlayed = timePlayed;
        }
    }
}
