using System.Collections.ObjectModel;

namespace Fix_It.Models
{
    // A maintenance issue report — persisted in Firestore, not SQLite.
    public class IssueReport
    {
        // Firestore's own document id, captured from the "name" field on read. Empty for a
        // report that hasn't been saved yet (e.g. while being built up in ReportIssueViewModel).
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        public bool HasPhoto => !string.IsNullOrEmpty(PhotoUrl);

        // "Open" or "Resolved" — no "In Progress" state, since there's no staff-assignment
        // workflow to justify a third stage without role-based accounts.
        public string Status { get; set; } = "Open";

        public bool IsResolved => Status == "Resolved";
        public bool IsOpen => !IsResolved;

        public string CreatedByFirebaseUid { get; set; } = string.Empty;

        // Denormalized at creation time (AuthSession.CurrentUser.Username) so "Submitted By"
        // can be shown without needing an Admin SDK to look up another user's email by uid.
        public string CreatedByEmail { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        // ObservableCollection rather than List — BindableLayout only re-renders on
        // INotifyCollectionChanged, so mutating a plain List in place (e.g. Add) would silently
        // never update the UI even though the underlying data changed. Newest-first order is
        // maintained by inserting new entries at index 0 (see FirebaseDataManager), not by
        // sorting here.
        public ObservableCollection<IssueActivityEntry> Activity { get; set; } = new();
    }
}
