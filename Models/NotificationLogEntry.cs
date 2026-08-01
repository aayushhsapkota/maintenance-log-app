namespace Fix_It.Models
{
    // A record of a notification that was fired (currently only "New Issue Reported"),
    // persisted to Firestore so the Notifications tab has history to show across devices and
    // app restarts — the system notification itself vanishes once dismissed from the OS tray.
    public class NotificationLogEntry
    {
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; }
    }
}
