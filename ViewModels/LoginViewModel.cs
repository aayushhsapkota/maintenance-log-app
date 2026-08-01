using System.Windows.Input;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs LoginPage only. Registration lives in RegisterViewModel/RegisterPage,
    // reached and returned from via the screen stack (PushAsync/PopAsync) *inside*
    // the modal layer that LoginPage was presented in (see IssueListPage.OnAppearing).
    public class LoginViewModel : BaseViewModel
    {
        // Preferences keys for the "Remember my details" checkbox.
        const string SavedEmailKey = "SavedEmail";
        const string SavedPasswordKey = "SavedPassword";

        readonly AuthSession _authSession;
        readonly INavigation _navigation;

        string _username = string.Empty;
        string _password = string.Empty;
        string _errorMessage = string.Empty;
        bool _rememberMe;
        bool _isBusy;

        public LoginViewModel(AuthSession authSession, INavigation navigation)
        {
            _authSession = authSession;
            _navigation = navigation;

            LoginCommand = new Command(async () => await LoginAsync());
            GoToRegisterCommand = new Command(async () => await _navigation.PushAsync(new RegisterPage()));

            // Pre-fill from Preferences if details were saved on a previous successful login.
            var savedEmail = Preferences.Get(SavedEmailKey, string.Empty);
            if (!string.IsNullOrEmpty(savedEmail))
            {
                _username = savedEmail;
                _password = Preferences.Get(SavedPasswordKey, string.Empty);
                _rememberMe = true;
            }
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand GoToRegisterCommand { get; }

        async Task LoginAsync()
        {
            if (_isBusy)
                return;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                return;
            }

            _isBusy = true;
            try
            {
                var success = await FirebaseAuthManager.Login(Username, Password);
                if (!success)
                {
                    ErrorMessage = "Invalid email or password.";
                    return;
                }

                if (RememberMe)
                {
                    Preferences.Set(SavedEmailKey, Username);
                    Preferences.Set(SavedPasswordKey, Password);
                }
                else
                {
                    Preferences.Default.Remove(SavedEmailKey);
                    Preferences.Default.Remove(SavedPasswordKey);
                }

                NotificationManager.ShowToast("Login successful!");

                // Hand the signed-in user off to IssueListPage via the shared session, then
                // close the ENTIRE modal layer (LoginPage + RegisterPage if it's on top of it)
                // in one call — PopModalAsync pops the whole NavigationPage that was pushed
                // modally, not just the current page within it.
                _authSession.CurrentUser = FirebaseAuthManager.CurrentUser;
                await _navigation.PopModalAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
