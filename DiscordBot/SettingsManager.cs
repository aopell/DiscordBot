using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBot
{
    public static class SettingsManager
    {
        private const string SettingsPath = Config.SettingsPath;

        private static JObject settings;

        /// <summary>
        /// Creates a JSON file for settings at <see cref="SettingsPath"/>
        /// </summary>
        private static void CreateSettingsFile()
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
        private static void LoadSettings()
        {
            if (settings == null)
            {
                CreateSettingsFile();
                string fileContents = File.ReadAllText(SettingsPath);
                settings = string.IsNullOrWhiteSpace(fileContents) ? new JObject() : JObject.Parse(fileContents);
            }
        }

        /// <summary>
        /// Add a new setting or update existing setting with the same name
        /// </summary>
        /// <param name="setting">Setting name</param>
        /// <param name="value">Setting value</param>
        public static void AddSetting(string setting, object value)
        {
            try
            {
                LoadSettings();
                if (settings[setting] == null && value != null)
                    settings.Add(setting, JToken.FromObject(value));
                else if (value == null && settings[setting] != null)
                    settings[setting] = null;
                else if (value != null)
                    settings[setting] = JToken.FromObject(value);
            }
            catch (Exception ex)
            {
                DiscordBot.LogEvent($"Error loading or saving settings. Please file a bug report with the following information:\n\n{ex}", DiscordBot.EventType.Error);
            }
        }


        /// <summary>
        /// Gets the value of the setting with the provided name if that setting has a boolean value
        /// </summary>
        /// <param name="setting">Setting with boolean value</param>
        /// <returns>Setting value or null if setting doesn't exist or is not a boolean</returns>
        public static bool? GetBooleanSetting(string setting)
        {
            try
            {
                LoadSettings();
                return settings[setting].Value<bool?>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the value of the setting with the provided name as type <c>T</c>
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="setting">Setting name</param>
        /// <returns>Tuple of (<see cref="bool"/> success,<c>T</c> value) where success is false when setting is not found</returns>
        public static (bool Success, T Value) GetSetting<T>(string setting)
        {
            try
            {
                LoadSettings();

                if (settings[setting] == null) return (false, default(T));

                return (true, settings[setting].Value<T>());
            }
            catch (Exception ex)
            {
                try
                {
                    return (true, settings[setting].ToObject<T>());
                }
                catch (Exception ex2)
                {
                    return (false, default(T));
                }
            }
        }

        /// <summary>
        /// Saves all queued settings to <see cref="SettingsPath"/>
        /// </summary>
        public static void SaveSettings()
        {
            File.WriteAllText(SettingsPath, settings.ToString(Formatting.Indented));
            settings = null;
        }


        /// <summary>
        /// Completely delete the settings file at <see cref="SettingsPath"/>. This action is irreversable.
        /// </summary>
        public static void DeleteSettings()
        {
            File.Delete(SettingsPath);
            settings = null;
        }
    }
}
