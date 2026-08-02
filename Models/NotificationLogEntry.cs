namespace Fix_It.Models
{
    // A fired notification, persisted to Firestore so the Notifications tab keeps history
    // after the system notification is dismissed from the OS tray.
    public class NotificationLogEntry
    {
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; }
    }
}
