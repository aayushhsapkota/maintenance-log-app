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

        // Always refresh from Firestore on appear — cheap single-document fetch, and it's what
        // picks up a saved edit when this page is popped back to, or someone else resolving the
        // same issue in the meantime.
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.RefreshAsync();
        }
    }
}
