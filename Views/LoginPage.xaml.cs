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

            // DatabaseService is registered once as a singleton in MauiProgram, so every page
            // that resolves it here shares the same SQLite connection instead of opening a new one.
            var databaseService = IPlatformApplication.Current!.Services.GetRequiredService<DatabaseService>();

            // Model instance created here and set as the page's BindingContext — the ViewModel
            // then drives everything the XAML above binds to.
            BindingContext = new LoginViewModel(databaseService, Navigation);
        }

        async void OnForgotPasswordTapped(object? sender, EventArgs e)
        {
            await this.DisplayAlertAsync("Forgot Password", "Please contact your system administrator to reset your password.", "OK");
        }
    }
}
