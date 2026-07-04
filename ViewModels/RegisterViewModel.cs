using System.Windows.Input;
using Fix_It.Services;

namespace Fix_It.ViewModels
{
    // Backs RegisterPage only. Reached from LoginPage via PushAsync; on success or via the
    // "Already have an account?" link it pops back to LoginPage rather than swapping a mode flag.
    public class RegisterViewModel : BaseViewModel
    {
        readonly DatabaseService _databaseService;
        readonly INavigation _navigation;

        string _username = string.Empty;
        string _password = string.Empty;
        string _confirmPassword = string.Empty;
        string _errorMessage = string.Empty;
        bool _isBusy;

        public RegisterViewModel(DatabaseService databaseService, INavigation navigation)
        {
            _databaseService = databaseService;
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
                ErrorMessage = "Username and password are required.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            _isBusy = true;
            try
            {
                var created = await _databaseService.RegisterUserAsync(Username, Password);
                if (!created)
                {
                    ErrorMessage = "That username is already taken.";
                    return;
                }

                // Account created — go back to Login so the user can sign in with it.
                await _navigation.PopAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
