using System;
using System.Collections.Generic;
using System.Text;
using AppUser = Fix_It.Models.User;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System.Threading.Tasks;


namespace Fix_It.Services
{
    public static class FirebaseAuthManager
    {
        private static readonly FirebaseAuthClient _authClient;

        // Keep track of the currently signed-in user
        public static AppUser? CurrentUser { get; set; }

        // Static constructor - runs once, the first time this class is used
        static FirebaseAuthManager()
        {
            _authClient = new FirebaseAuthClient(new FirebaseAuthConfig()
            {
                ApiKey = "AIzaSyB9PhdoK644hSrkKl4RsfFChQha6qooNu8",
                AuthDomain = "fix-it-765d8.firebaseapp.com",
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                }
            });
        }

        // Register an account with Firebase
        public async static Task<bool> RegisterAccount(AppUser user, string password)
        {
            try
            {
                var credentials = await _authClient
                    .CreateUserWithEmailAndPasswordAsync(user.Username, password);
                var id = credentials.User.Uid;
                user.FirebaseUid = id;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration failed: {ex.Message}");
                return false;
              
            }
        }

        // Try to log in with Firebase
        public async static Task<bool> Login(string email, string password)
        {
            try
            {
                var credentials = await _authClient
                    .SignInWithEmailAndPasswordAsync(email, password);
                var id = credentials.User.Uid;

                CurrentUser = new AppUser()
                {
                    FirebaseUid = id,
                    Username = email
                };
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");
                return false;
            }
        }
    }
}
