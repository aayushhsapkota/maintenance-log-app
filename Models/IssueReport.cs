namespace Fix_It.Models
{
    // A maintenance issue report — persisted in Firestore now, not SQLite, so there's no
    // local int id (Firestore generates its own document id, which we don't need to track).
    public class IssueReport
    {
        public string Title { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        public string CreatedByFirebaseUid { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
