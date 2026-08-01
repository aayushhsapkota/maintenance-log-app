namespace Fix_It.Models
{
    // One entry in an IssueReport's activity log (e.g. "reported the issue",
    // "marked this issue resolved") — embedded on the report document itself in Firestore,
    // not a separate subcollection.
    public class IssueActivityEntry
    {
        public string ActorEmail { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; }
    }
}
