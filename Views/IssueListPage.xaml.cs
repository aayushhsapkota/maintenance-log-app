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

            // The login modal itself is presented from App.xaml.cs (Window.Created) as early as
            // possible in the app's lifecycle, not from here. This just refreshes once someone's
            // actually signed in — which happens both right after the modal closes (PopModalAsync
            // reveals this page) and after returning from ReportIssuePage.
            if (_viewModel.CurrentUser is not null)
                await _viewModel.LoadReportsAsync();
        }
    }
}
