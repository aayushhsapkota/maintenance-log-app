using Fix_It.Models;

namespace Fix_It.Services
{
    // Tiny shared holder for "who is currently signed in." LoginViewModel sets this right
    // before popping the login modal; IssueListViewModel reads it to decide whether to
    // present the login modal or load data, and to tag/scope reports by user. Registered as
    // a singleton so every page resolves the same instance instead of passing a User around
    // through constructors.
    public class AuthSession
    {
        public User? CurrentUser { get; set; }
    }
}
