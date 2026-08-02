using Fix_It.Models;
using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class IssueDetailPage : ContentPage
    {
        readonly IssueDetailViewModel _viewModel;

        public IssueDetailPage(IssueReport report)
        {
            InitializeComponent();

            var authSession = IPlatformApplication.Current!.Services.GetRequiredService<AuthSession>();
            _viewModel = new IssueDetailViewModel(this, authSession, report);
            BindingContext = _viewModel;
        }

        // Refresh from Firestore on appear, so a saved edit or someone else resolving
        // the issue shows up when this page is returned to.
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.RefreshAsync();
        }
    }
}
