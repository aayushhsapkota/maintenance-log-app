using System.Security.Cryptography;

namespace Fix_It.Services
{
    public static class PasswordHasher
    {
        const int SaltSizeBytes = 16;
        const int KeySizeBytes = 32;
        const int Iterations = 100_000;
        static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        public static (string Hash, string Salt) HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySizeBytes);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            byte[] salt = Convert.FromBase64String(storedSalt);
            byte[] expectedHash = Convert.FromBase64String(storedHash);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySizeBytes);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
