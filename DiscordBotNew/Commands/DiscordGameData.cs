using System;

namespace DiscordBotNew
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
