using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Gamer
    {
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
            if (!File.Exists(Config.GameDataPath))
                File.Create(Config.GameDataPath).Close();
            File.WriteAllText(Config.GameDataPath, JsonConvert.SerializeObject(gamers));
        }

        private static List<Gamer> LoadGamers()
        {
            if (!File.Exists(Config.GameDataPath))
                File.Create(Config.GameDataPath).Close();

            return JsonConvert.DeserializeObject<List<Gamer>>(File.ReadAllText(Config.GameDataPath));
        }

        public static async Task GameStarted(User user)
        {
            await Task.Run(() =>
            {
                var gamers = LoadGamers();
                if (gamers == null) gamers = new List<Gamer>();

                Gamer person = null;

                foreach (Gamer g in gamers)
                {
                    if (g.UserId == user.Id)
                        person = g;
                }

                if (person == null)
                    person = new Gamer(user.Name, user.Discriminator, user.Id);

                person.StartTracking();

                var duplicates = (from g in gamers where g.UserId == person.UserId select g).ToList();

                for (int i = 0; i < duplicates.Count; i++)
                    gamers.Remove(duplicates[i]);

                gamers.Add(person);

                SaveGamers(gamers);
            });
        }

        public static async Task<TimeSpan> GameStopped(User user, string game)
        {
            return await Task.Run(() =>
            {
                if (game == null) return TimeSpan.Zero;

                var gamers = LoadGamers();
                if (gamers == null) gamers = new List<Gamer>();

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

                for (int i = 0; i < duplicates.Count; i++)
                    gamers.Remove(duplicates[i]);

                gamers.Add(person);

                SaveGamers(gamers);

                return time;
            });
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
            foreach (var g in GamesPlayed[date])
                if (g.Game == game)
                    return g;

            return null;
        }

        public static Gamer FindById(ulong userId)
        {
            var gamers = LoadGamers();
            foreach (var g in gamers)
                if (g.UserId == userId)
                    return g;
            return null;
        }
    }
}
