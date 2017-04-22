using System;

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
