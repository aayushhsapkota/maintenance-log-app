using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();

            var databaseService = IPlatformApplication.Current!.Services.GetRequiredService<DatabaseService>();
            BindingContext = new RegisterViewModel(databaseService, Navigation);
        }
    }
}
