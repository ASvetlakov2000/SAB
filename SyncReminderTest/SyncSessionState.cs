using System;

namespace SyncReminderTest
{
    internal class SyncSessionState
    {
        public string DocumentKey { get; set; }

        public string DocumentTitle { get; set; }

        public DateTime LastSuccessfulSyncTime { get; set; }
    }
}
