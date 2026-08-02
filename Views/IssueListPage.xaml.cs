using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class IssueListPage : ContentPage
    {
        readonly IssueListViewModel _viewModel;

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

            // The login modal itself is presented from App.xaml.cs, not here — this just
            // refreshes once someone's actually signed in.
            if (_viewModel.CurrentUser is not null)
            {
                await _viewModel.LoadReportsAsync();
                _viewModel.StartPolling();
            }
        }

        // Only poll while this page is actually visible, not while a sub-page covers it.
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.StopPolling();
        }
    }
}
