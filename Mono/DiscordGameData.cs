using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class DiscordGameData
    {
        public DiscordGame Game;
        public TimeSpan TimePlayed;

        public DiscordGameData(DiscordGame game, TimeSpan timePlayed)
        {
            Game = game;
            TimePlayed = timePlayed;
        }
    }
}
