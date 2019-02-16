using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DiscordBotNew.Settings
{
    public static class ConfigFileManager
    {
        public static void LoadConfigFiles(DiscordBot bot)
        {
            var configs = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass && x.Namespace == "DiscordBotNew.Settings.Models" && x.IsSubclassOf(typeof(Config)));
            foreach (var property in bot.GetType().GetProperties().Where(x => x.PropertyType.IsSubclassOf(typeof(Config))))
            {
                string filePath = Config.BasePath + property.PropertyType.GetCustomAttribute<ConfigFileAttribute>().FileName;

                if (!File.Exists(filePath)) continue;
                object c = JsonConvert.DeserializeObject(File.ReadAllText(filePath), property.PropertyType) ?? Activator.CreateInstance(property.PropertyType);
                property.SetValue(bot, c);
            }
        }
    }
}
