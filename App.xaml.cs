using Fix_It.Services;
using Fix_It.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Hooked here instead of IssueListPage.OnAppearing to avoid a brief flash of the
            // empty list before the modal covers it — Window.Created fires earlier.
            window.Created += async (_, _) => await PresentLoginIfNeededAsync(window);

            return window;
        }

        static async Task PresentLoginIfNeededAsync(Window window)
        {
            var authSession = IPlatformApplication.Current!.Services.GetRequiredService<AuthSession>();
            if (authSession.CurrentUser is not null)
                return;

            await window.Page!.Navigation.PushModalAsync(new NavigationPage(new LoginPage()));
        }
    }
}
