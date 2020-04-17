using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings
{
    public abstract class Config
    {
        [JsonIgnore]
        public const string BasePath = "";

        private ReaderWriterLockSlim rwLock { get; }
        private string fileName => BasePath + GetType().GetCustomAttribute<ConfigFileAttribute>().FileName;

        protected Config()
        {
            rwLock = new ReaderWriterLockSlim();
        }

        public void SaveConfig()
        {
            using (new WriteLock(rwLock))
            {
                File.WriteAllText(fileName, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }
    }
}
