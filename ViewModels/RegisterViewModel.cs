using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;

namespace Fix_It.ViewModels
{
    // Backs RegisterPage only. Reached from LoginPage via PushAsync; on success or via the
    // "Already have an account?" link it pops back to LoginPage rather than swapping a mode flag.
    // Doesn't sign the user in itself, so it has no need for AuthSession — just Firebase account
    // creation, then back to Login to sign in with the new account.
    public class RegisterViewModel : BaseViewModel
    {
        readonly INavigation _navigation;

        string _username = string.Empty;
        string _password = string.Empty;
        string _confirmPassword = string.Empty;
        string _errorMessage = string.Empty;
        bool _isBusy;

        public RegisterViewModel(INavigation navigation)
        {
            _navigation = navigation;

            RegisterCommand = new Command(async () => await RegisterAsync());
            GoToLoginCommand = new Command(async () => await _navigation.PopAsync());
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

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
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

        public ICommand RegisterCommand { get; }
        public ICommand GoToLoginCommand { get; }

        async Task RegisterAsync()
        {
            if (_isBusy)
                return;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            // Firebase itself enforces a 6-character minimum — checking here first avoids a
            // round trip for the most common validation failure.
            if (Password.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters.";
                return;
            }

            _isBusy = true;
            try
            {
                var user = new User { Username = Username };
                var created = await FirebaseAuthManager.RegisterAccount(user, Password);
                if (!created)
                {
                    ErrorMessage = "Registration failed. The email may already be in use, or is invalid.";
                    return;
                }

                // Account created — go back to Login so the user can sign in with it.
                await ShowToastAsync("Account created successfully!");
                await _navigation.PopAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
