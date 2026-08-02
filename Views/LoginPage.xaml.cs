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

            var authSession = IPlatformApplication.Current!.Services.GetRequiredService<AuthSession>();
            BindingContext = new LoginViewModel(authSession, Navigation);
        }

        async void OnForgotPasswordTapped(object? sender, EventArgs e)
        {
            await this.DisplayAlertAsync("Forgot Password", "Please contact your system administrator to reset your password.", "OK");
        }

        // Presented modally as an auth wall over IssueListPage — block the hardware back
        // button so it can't be dismissed that way. RegisterPage isn't blocked; backing out
        // of Register to Login is fine.
        protected override bool OnBackButtonPressed() => true;
    }
}
