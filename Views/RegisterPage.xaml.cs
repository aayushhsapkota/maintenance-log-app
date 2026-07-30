using Fix_It.ViewModels;

namespace Fix_It.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();

            BindingContext = new RegisterViewModel(Navigation);
        }
    }
}
