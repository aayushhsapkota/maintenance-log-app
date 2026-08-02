using System.Collections.ObjectModel;

namespace Fix_It.Models
{
    // A maintenance issue report — persisted in Firestore, not SQLite.
    public class IssueReport
    {
        // Firestore's document id, captured from the "name" field on read. Empty until saved.
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        public bool HasPhoto => !string.IsNullOrEmpty(PhotoUrl);

        // "Open" or "Resolved" — no "In Progress" state without a staff-assignment workflow.
        public string Status { get; set; } = "Open";

        public bool IsResolved => Status == "Resolved";
        public bool IsOpen => !IsResolved;

        public string CreatedByFirebaseUid { get; set; } = string.Empty;

        // Denormalized at creation time so "Submitted By" can show without an Admin SDK
        // lookup of another user's email by uid.
        public string CreatedByEmail { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        // ObservableCollection, not List — BindableLayout only re-renders on
        // INotifyCollectionChanged. Newest-first order comes from inserting at index 0
        // (see FirebaseDataManager), not from sorting here.
        public ObservableCollection<IssueActivityEntry> Activity { get; set; } = new();
    }
}
