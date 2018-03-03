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
        private static List<object> ConfigFiles { get; }

        static ConfigFileManager()
        {
            ConfigFiles = new List<object>();
            var configs = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass && x.Namespace == "DiscordBotNew.Settings.Models" && x.IsSubclassOf(typeof(Config)));
            foreach (var config in configs)
            {
                object c = JsonConvert.DeserializeObject(File.ReadAllText(Config.BasePath + config.GetCustomAttribute<ConfigFileAttribute>().FileName), config);
                ConfigFiles.Add(c);
            }
        }

        public static T GetConfig<T>() where T : Config
        {
            return (T)ConfigFiles.FirstOrDefault(x => typeof(T) == x.GetType());
        }
    }
}
