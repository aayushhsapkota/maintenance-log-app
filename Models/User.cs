namespace Fix_It.Models
{
    // Identity lives in Firebase Authentication — this is just a lightweight carrier
    // for the signed-in user's data.
    public class User
    {
        public string Username { get; set; } = string.Empty; // must be a valid email (Firebase requirement)
        public string FirebaseUid { get; set; } = string.Empty;
    }
}
