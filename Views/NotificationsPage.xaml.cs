using Fix_It.ViewModels;

namespace Fix_It.Views
{
    public partial class NotificationsPage : ContentPage
    {
        readonly NotificationsViewModel _viewModel;

        public NotificationsPage()
        {
            InitializeComponent();

            _viewModel = new NotificationsViewModel();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadNotificationsAsync();
        }
    }
}
