using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();

            // AuthSession is registered as a singleton in MauiProgram, so every page that
            // resolves it here shares the same instance.
            var authSession = IPlatformApplication.Current!.Services.GetRequiredService<AuthSession>();

            // Model instance created here and set as the page's BindingContext — the ViewModel
            // then drives everything the XAML above binds to.
            BindingContext = new LoginViewModel(authSession, Navigation);
        }

        async void OnForgotPasswordTapped(object? sender, EventArgs e)
        {
            await this.DisplayAlertAsync("Forgot Password", "Please contact your system administrator to reset your password.", "OK");
        }

        // LoginPage is presented modally as the root of a NavigationPage pushed via
        // PushModalAsync (see IssueListPage.OnAppearing) — an auth wall over IssueListPage.
        // Returning true here swallows the hardware/system back button so it can't be
        // dismissed that way; RegisterPage (pushed on top of this) is NOT blocked, since
        // backing out of Register to Login is fine.
        protected override bool OnBackButtonPressed() => true;
    }
}
