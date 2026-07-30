namespace Fix_It.Models
{
    // Identity now lives in Firebase Authentication — this is just a lightweight carrier for
    // the signed-in user's data, not a locally persisted SQLite entity anymore.
    public class User
    {
        public string Username { get; set; } = string.Empty; // must be a valid email (Firebase requirement)
        public string FirebaseUid { get; set; } = string.Empty;
    }
}
