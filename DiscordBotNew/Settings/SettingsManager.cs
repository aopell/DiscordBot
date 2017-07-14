using System;
using System.IO;
using System.Threading;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBotNew.Settings
{
    public class SettingsManager
    {
        public const string BasePath = "D:\\home\\data\\jobs\\continuous\\NetcatBot\\";
        private string SettingsPath { get; }
        private ReaderWriterLockSlim rwLock { get; }

        private JObject settings;

        public SettingsManager(string settingsPath)
        {
            rwLock = new ReaderWriterLockSlim();
            SettingsPath = settingsPath;
            LoadSettings();
        }

        /// <summary>
        /// Creates a JSON file for settings at <see cref="SettingsPath"/>
        /// </summary>
        private void CreateSettingsFile()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                    File.Create(SettingsPath).Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new FileNotFoundException($"The provided {nameof(SettingsPath)} was invalid", ex);
            }
        }

        /// <summary>
        /// Loads the contents of the settings file into <see cref="settings"/>
        /// </summary>
        private void LoadSettings()
        {
            if (settings == null)
            {
                using (new WriteLock(rwLock))
                {
                    CreateSettingsFile();
                    string fileContents = File.ReadAllText(SettingsPath);
                    settings = string.IsNullOrWhiteSpace(fileContents) ? new JObject() : JObject.Parse(fileContents);
                }
            }
        }

        /// <summary>
        /// Add a new setting or update existing setting with the same name
        /// </summary>
        /// <param name="setting">Setting name</param>
        /// <param name="value">Setting value</param>
        public void AddSetting(string setting, object value)
        {
            try
            {
                using (new WriteLock(rwLock))
                {
                    LoadSettings();
                    if (settings[setting] == null && value != null)
                        settings.Add(setting, JToken.FromObject(value));
                    else if (value == null && settings[setting] != null)
                        settings[setting] = null;
                    else if (value != null)
                        settings[setting] = JToken.FromObject(value);
                }
            }
            catch (Exception ex)
            {
                DiscordBot.Log(new Discord.LogMessage(LogSeverity.Error, nameof(AddSetting), "Error loading or saving settings. Please file a bug report with the following information", ex));
            }
        }

        /// <summary>
        /// Gets the value of the setting with the provided name as type <c>T</c>
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="setting">Setting name</param>
        /// <param name="result">Result value</param>
        /// <returns>True when setting was successfully found, false when setting is not found</returns>
        public bool GetSetting<T>(string setting, out T result)
        {
            result = default(T);

            try
            {
                LoadSettings();

                using (new ReadLock(rwLock))
                {
                    if (settings[setting] == null) return false;

                    result = settings[setting].ToObject<T>();
                }
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    using (new ReadLock(rwLock))
                    {
                        result = settings[setting].Value<T>();
                    }
                    return true;
                }
                catch (Exception ex2)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Saves all queued settings to <see cref="SettingsPath"/>
        /// </summary>
        public void SaveSettings()
        {
            using (new WriteLock(rwLock))
            {
                File.WriteAllText(SettingsPath, settings.ToString(Formatting.Indented));
            }
        }


        /// <summary>
        /// Completely delete the settings file at <see cref="SettingsPath"/>. This action is irreversable.
        /// </summary>
        public void DeleteSettings()
        {
            using (new WriteLock(rwLock))
            {
                File.Delete(SettingsPath);
                settings = null;
            }
        }
    }
}