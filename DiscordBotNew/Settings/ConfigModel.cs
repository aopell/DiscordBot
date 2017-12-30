using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace DiscordBotNew.Settings
{
    public abstract class ConfigModel
    {
        [JsonIgnore]
        public const string BasePath = "D:\\home\\data\\jobs\\continuous\\NetcatBot\\";
        [JsonIgnore]
        protected string FilePath { get; private set; }
        private ReaderWriterLockSlim rwLock { get; }

        public ConfigModel()
        {
            rwLock = new ReaderWriterLockSlim();
        }

        public void SaveSettings()
        {
            using (new WriteLock(rwLock))
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public static T LoadSettings<T>(string path) where T : ConfigModel
        {
            try
            {
                if (!File.Exists(BasePath + path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(BasePath + path));
                    File.Create(BasePath + path).Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new FileNotFoundException($"The provided file path was invalid", ex);
            }

            try
            {
                T settingsModel = JsonConvert.DeserializeObject<T>(File.ReadAllText(BasePath + path));
                settingsModel.FilePath = BasePath + path;
                return settingsModel;
            }
            catch (Exception ex)
            {
                throw new FormatException("The provided file was not in the correct format, please ensure all required fields are present", ex);
            }
        }
    }
}
