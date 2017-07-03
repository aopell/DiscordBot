using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Discord.WebSocket;

namespace DiscordBotNew
{
    public class Gamer
    {
        private const string GameDataPath = SettingsManager.BasePath + "gamedata.json";

        public string Username;
        public int Descriminator;
        public ulong UserId;
        public DateTime? LastGameStarted;
        public Dictionary<DateTime, List<DiscordGameData>> GamesPlayed;

        public Gamer() { }

        private Gamer(string username, int descriminator, ulong id)
        {
            Username = username;
            Descriminator = descriminator;
            UserId = id;
            LastGameStarted = null;
            GamesPlayed = new Dictionary<DateTime, List<DiscordGameData>>();
        }

        private static void SaveGamers(List<Gamer> gamers)
        {
            if (!File.Exists(GameDataPath))
                File.Create(GameDataPath).Close();
            File.WriteAllText(GameDataPath, JsonConvert.SerializeObject(gamers));
        }

        private static List<Gamer> LoadGamers()
        {
            if (!File.Exists(GameDataPath))
                File.Create(GameDataPath).Close();

            return JsonConvert.DeserializeObject<List<Gamer>>(File.ReadAllText(GameDataPath));
        }

        public static void GameStarted(SocketUser user)
        {
            var gamers = LoadGamers() ?? new List<Gamer>();

            Gamer person = null;

            foreach (Gamer g in gamers.Where(g => g.UserId == user.Id))
            {
                person = g;
            }

            if (person == null)
                person = new Gamer(user.Username, user.DiscriminatorValue, user.Id);

            person.StartTracking();

            gamers.RemoveAll(g => g.UserId == person.UserId);

            gamers.Add(person);

            SaveGamers(gamers);
        }

        public static TimeSpan GameStopped(SocketUser user, string game)
        {
            if (game == null) return TimeSpan.Zero;

            var gamers = LoadGamers() ?? new List<Gamer>();

            Gamer person = null;

            foreach (Gamer g in gamers)
            {
                if (user.Id == g.UserId)
                {
                    person = g;
                    break;
                }
            }

            if (person == null) return TimeSpan.Zero;

            var time = person.StopTracking(game);

            var duplicates = (from g in gamers where g.UserId == person.UserId select g).ToList();

            foreach (Gamer t in duplicates)
                gamers.Remove(t);

            gamers.Add(person);

            SaveGamers(gamers);

            return time;
        }

        private void StartTracking()
        {
            LastGameStarted = DateTime.Now;
        }

        private TimeSpan StopTracking(string game)
        {
            if (!GamesPlayed.ContainsKey(DateTime.Today))
                GamesPlayed.Add(DateTime.Today, new List<DiscordGameData>());

            var data = GameDataListContains(DateTime.Today, game);

            TimeSpan time = (LastGameStarted.HasValue ? DateTime.Now - LastGameStarted.Value : TimeSpan.Zero);

            if (data != null && time != TimeSpan.Zero)
                data.TimePlayed += time;
            else
                GamesPlayed[DateTime.Today].Add(new DiscordGameData(game, time));

            LastGameStarted = null;

            return time;
        }

        private DiscordGameData GameDataListContains(DateTime date, string game)
        {
            return GamesPlayed[date].FirstOrDefault(g => g.Game == game);
        }

        public static Gamer FindById(ulong userId)
        {
            var gamers = LoadGamers();
            return gamers.FirstOrDefault(g => g.UserId == userId);
        }
    }
}
