using System;

namespace SAB.SyncReminder
{
    internal class SyncSessionState
    {
        public string DocumentKey { get; set; }

        public string DocumentTitle { get; set; }

        public DateTime LastSuccessfulSyncTime { get; set; }

        public bool IsReminderDismissed { get; set; }
    }
}
