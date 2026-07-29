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
        readonly DatabaseService _databaseService;
        readonly AuthSession _authSession;
        readonly INavigation _navigation;

        string _username = string.Empty;
        string _password = string.Empty;
        string _errorMessage = string.Empty;
        bool _isBusy;

        public LoginViewModel(DatabaseService databaseService, AuthSession authSession, INavigation navigation)
        {
            _databaseService = databaseService;
            _authSession = authSession;
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

                // Hand the signed-in user off to IssueListPage via the shared session, then
                // close the ENTIRE modal layer (LoginPage + RegisterPage if it's on top of it)
                // in one call — PopModalAsync pops the whole NavigationPage that was pushed
                // modally, not just the current page within it.
                _authSession.CurrentUser = user;
                await _navigation.PopModalAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
