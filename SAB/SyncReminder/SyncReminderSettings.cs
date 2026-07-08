namespace SAB.SyncReminder
{
    internal class SyncReminderSettings
    {
        public bool IsEnabled { get; set; }

        public int ReminderDelayMinutes { get; set; }

        public SyncReminderAnimationMode AnimationMode { get; set; }

        public static SyncReminderSettings CreateDefault()
        {
            SyncReminderSettings settings = new SyncReminderSettings();
            settings.IsEnabled = true;
            settings.ReminderDelayMinutes = 60;
            settings.AnimationMode = SyncReminderAnimationMode.DuckOnly;
            return settings;
        }

        public SyncReminderSettings Clone()
        {
            SyncReminderSettings settings = new SyncReminderSettings();
            settings.IsEnabled = IsEnabled;
            settings.ReminderDelayMinutes = ReminderDelayMinutes;
            settings.AnimationMode = AnimationMode;
            return settings;
        }
    }
}
