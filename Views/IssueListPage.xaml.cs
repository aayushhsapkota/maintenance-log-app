using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class IssueListPage : ContentPage
    {
        readonly IssueListViewModel _viewModel;
        bool _isPresentingLogin;

        public IssueListPage()
        {
            InitializeComponent();

            var authSession = IPlatformApplication.Current!.Services.GetRequiredService<AuthSession>();
            _viewModel = new IssueListViewModel(this, authSession);
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_viewModel.CurrentUser is null)
            {
                // Not signed in yet (this is app start) — present the auth flow modally,
                // wrapped in its own NavigationPage so Login -> Register inside it can use
                // normal PushAsync/PopAsync. Guarded by the flag so a stray extra OnAppearing
                // call can't push a second login modal on top of the first.
                if (_isPresentingLogin)
                    return;

                _isPresentingLogin = true;
                await Navigation.PushModalAsync(new NavigationPage(new LoginPage()));
                _isPresentingLogin = false;
            }
            else
            {
                // Fires again once PopModalAsync reveals this page after a successful login,
                // or after returning from ReportIssuePage — refresh either way.
                await _viewModel.LoadReportsAsync();
            }
        }
    }
}
