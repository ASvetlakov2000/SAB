using System;
using System.IO;
using System.Xml.Linq;

namespace SAB.SyncReminder
{
    internal class SyncReminderSettingsService
    {
        private const int MinimumMinutes = 1;
        private const int MaximumMinutes = 720;

        public SyncReminderSettings Load()
        {
            string filePath = GetSettingsFilePath();
            if (!File.Exists(filePath))
            {
                return SyncReminderSettings.CreateDefault();
            }

            try
            {
                XDocument document = XDocument.Load(filePath);
                XElement root = document.Element("SyncReminderSettings");
                if (root == null)
                {
                    return SyncReminderSettings.CreateDefault();
                }

                SyncReminderSettings settings = SyncReminderSettings.CreateDefault();
                settings.IsEnabled = ReadBool(root, "IsEnabled", settings.IsEnabled);
                settings.ReminderDelayMinutes = ReadInt(root, "ReminderDelayMinutes", settings.ReminderDelayMinutes);
                settings.ReminderDelayMinutes = Clamp(settings.ReminderDelayMinutes, MinimumMinutes, MaximumMinutes);
                settings.AnimationMode = ReadAnimationMode(root, "AnimationMode", settings.AnimationMode);
                return settings;
            }
            catch
            {
                return SyncReminderSettings.CreateDefault();
            }
        }

        public void Save(SyncReminderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            string folderPath = GetSettingsFolderPath();
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            SyncReminderSettings normalizedSettings = settings.Clone();
            normalizedSettings.ReminderDelayMinutes = Clamp(normalizedSettings.ReminderDelayMinutes, MinimumMinutes, MaximumMinutes);

            XDocument document = new XDocument(
                new XElement(
                    "SyncReminderSettings",
                    new XElement("IsEnabled", normalizedSettings.IsEnabled),
                    new XElement("ReminderDelayMinutes", normalizedSettings.ReminderDelayMinutes),
                    new XElement("AnimationMode", normalizedSettings.AnimationMode.ToString())));

            document.Save(GetSettingsFilePath());
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsFolderPath(), "settings.xml");
        }

        private static string GetSettingsFolderPath()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataFolder, "SAB", "SyncReminder");
        }

        private static bool ReadBool(XElement root, string elementName, bool defaultValue)
        {
            XElement element = root.Element(elementName);
            if (element == null)
            {
                return defaultValue;
            }

            bool value;
            if (!bool.TryParse(element.Value, out value))
            {
                return defaultValue;
            }

            return value;
        }

        private static int ReadInt(XElement root, string elementName, int defaultValue)
        {
            XElement element = root.Element(elementName);
            if (element == null)
            {
                return defaultValue;
            }

            int value;
            if (!int.TryParse(element.Value, out value))
            {
                return defaultValue;
            }

            return value;
        }

        private static SyncReminderAnimationMode ReadAnimationMode(
            XElement root,
            string elementName,
            SyncReminderAnimationMode defaultValue)
        {
            XElement element = root.Element(elementName);
            if (element == null)
            {
                return defaultValue;
            }

            SyncReminderAnimationMode value;
            if (!Enum.TryParse(element.Value, true, out value))
            {
                return defaultValue;
            }

            if (!Enum.IsDefined(typeof(SyncReminderAnimationMode), value))
            {
                return defaultValue;
            }

            return value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
