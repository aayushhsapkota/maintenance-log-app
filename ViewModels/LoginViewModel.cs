using System.Windows.Input;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs LoginPage only. Registration lives in RegisterViewModel/RegisterPage,
    // reached and returned from via the screen stack (PushAsync/PopAsync).
    public class LoginViewModel : BaseViewModel
    {
        readonly DatabaseService _databaseService;
        readonly INavigation _navigation;

        string _username = string.Empty;
        string _password = string.Empty;
        string _errorMessage = string.Empty;
        bool _isBusy;

        public LoginViewModel(DatabaseService databaseService, INavigation navigation)
        {
            _databaseService = databaseService;
            _navigation = navigation;

            LoginCommand = new Command(async () => await LoginAsync());
            GoToRegisterCommand = new Command(async () => await _navigation.PushAsync(new RegisterPage()));
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

        public ICommand LoginCommand { get; }
        public ICommand GoToRegisterCommand { get; }

        async Task LoginAsync()
        {
            if (_isBusy)
                return;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Username and password are required.";
                return;
            }

            _isBusy = true;
            try
            {
                var user = await _databaseService.ValidateUserAsync(Username, Password);
                if (user is null)
                {
                    ErrorMessage = "Invalid username or password.";
                    return;
                }

                await _navigation.PushAsync(new ReportIssuePage());
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
