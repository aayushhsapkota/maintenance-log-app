using Fix_It.Models;
using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class IssueListPage : ContentPage
    {
        readonly IssueListViewModel _viewModel;

        public IssueListPage(User currentUser)
        {
            InitializeComponent();

            var databaseService = IPlatformApplication.Current!.Services.GetRequiredService<DatabaseService>();
            _viewModel = new IssueListViewModel(databaseService, this, currentUser);
            BindingContext = _viewModel;
        }

        // Refresh on every appearance, not just the first — this is what picks up a report
        // that was just submitted and popped back to this page.
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadReportsAsync();
        }
    }
}
