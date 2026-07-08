namespace SyncReminderTest
{
    internal class ReminderSettings
    {
        public bool IsEnabled { get; set; }

        public int ReminderDelayMinutes { get; set; }

        public ReminderAnimationMode AnimationMode { get; set; }

        public static ReminderSettings CreateDefault()
        {
            ReminderSettings settings = new ReminderSettings();
            settings.IsEnabled = true;
            settings.ReminderDelayMinutes = 1;
            settings.AnimationMode = ReminderAnimationMode.DuckOnly;
            return settings;
        }

        public ReminderSettings Clone()
        {
            ReminderSettings settings = new ReminderSettings();
            settings.IsEnabled = IsEnabled;
            settings.ReminderDelayMinutes = ReminderDelayMinutes;
            settings.AnimationMode = AnimationMode;
            return settings;
        }
    }
}
