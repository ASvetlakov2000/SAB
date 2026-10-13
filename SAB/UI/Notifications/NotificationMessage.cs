using System;

namespace SAB.UI.Notifications
{
    public enum NotificationTone { Information, Success, Warning, Error }

    /// <summary>Immutable presentation data. Contains no Revit objects or callbacks.</summary>
    public sealed class NotificationMessage
    {
        public string Module { get; }
        public string Outcome { get; }
        public NotificationTone Tone { get; }
        public string Summary { get; }
        public string ElementIds { get; }
        public string Support { get; }
        public string Warnings { get; }
        public NotificationMessage(string module, string outcome, NotificationTone tone, string summary, string elementIds = null, string support = null, string warnings = null)
        { Module = module; Outcome = outcome; Tone = tone; Summary = summary ?? ""; ElementIds = elementIds; Support = support; Warnings = warnings; }
    }

    /// <summary>Monotonic active time; pausing never resets the remaining duration.</summary>
    public sealed class NotificationLifetime
    {
        public const double DurationSeconds = 10;
        public double RemainingSeconds { get; private set; } = DurationSeconds;
        private double last;
        private bool paused = true;
        public void Update(double now, bool pause)
        {
            if (!paused) RemainingSeconds = Math.Max(0, RemainingSeconds - Math.Max(0, now - last));
            last = now; paused = pause;
        }
    }
}
