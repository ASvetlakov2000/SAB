namespace SAB.SyncReminder
{
    internal class SyncReminderSettings
    {
        public bool IsEnabled { get; set; }

        public int ReminderDelayMinutes { get; set; }

        public static SyncReminderSettings CreateDefault()
        {
            SyncReminderSettings settings = new SyncReminderSettings();
            settings.IsEnabled = true;
            settings.ReminderDelayMinutes = 60;
            return settings;
        }

        public SyncReminderSettings Clone()
        {
            SyncReminderSettings settings = new SyncReminderSettings();
            settings.IsEnabled = IsEnabled;
            settings.ReminderDelayMinutes = ReminderDelayMinutes;
            return settings;
        }
    }
}
