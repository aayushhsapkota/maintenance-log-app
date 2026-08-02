using Fix_It.Models;

namespace Fix_It.Services
{
    // Shared holder for "who is currently signed in." LoginViewModel sets it before
    // closing the login modal; IssueListViewModel reads it to decide whether to show the
    // modal or load data. Registered as a singleton so every page shares the same instance.
    public class AuthSession
    {
        public User? CurrentUser { get; set; }
    }
}
