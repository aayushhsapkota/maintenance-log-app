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

            // Present the login modal as early as possible in the window's lifecycle. Doing
            // this from IssueListPage.OnAppearing instead (as before) meant it only ran after
            // that page had already been laid out and shown, which was visible as a brief flash
            // of the empty list before the modal covered it. Hooking Window.Created gets the
            // push started right after the window exists, well before OnAppearing would fire.
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
