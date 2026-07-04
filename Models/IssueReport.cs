using SQLite;

namespace Fix_It.Models
{
    public class IssueReport
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string Title { get; set; } = string.Empty;

        [NotNull]
        public string Location { get; set; } = string.Empty;

        [NotNull]
        public string Description { get; set; } = string.Empty;

        [NotNull]
        public string Priority { get; set; } = string.Empty;

        public string? PhotoPath { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
