using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings
{
    public static class ConfigFileManager
    {
        public static void LoadConfigFiles(DiscordBot bot)
        {
            var configs = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass && x.Namespace == "DiscordBotNew.Settings.Models" && x.IsSubclassOf(typeof(Config)));
            foreach (var property in bot.GetType().GetProperties().Where(x => x.PropertyType.IsSubclassOf(typeof(Config))))
            {
                object c = JsonConvert.DeserializeObject(File.ReadAllText(Config.BasePath + property.PropertyType.GetCustomAttribute<ConfigFileAttribute>().FileName), property.PropertyType);
                property.SetValue(bot, c);
            }
        }
    }
}
