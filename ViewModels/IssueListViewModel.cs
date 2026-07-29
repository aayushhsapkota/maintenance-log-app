using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueListPage — the app's root page. A simple read-only list of the reports the
    // signed-in employee has submitted (title, location, priority, submitted date). No status
    // workflow, stats, or edit/close actions here; that's maintenance-staff territory and out
    // of scope.
    public class IssueListViewModel : BaseViewModel
    {
        readonly DatabaseService _databaseService;
        readonly Page _page;
        readonly AuthSession _authSession;

        bool _isLoading;

        public IssueListViewModel(DatabaseService databaseService, Page page, AuthSession authSession)
        {
            _databaseService = databaseService;
            _page = page;
            _authSession = authSession;

            GoToReportCommand = new Command(async () =>
            {
                if (_authSession.CurrentUser is null)
                    return;

                await _page.Navigation.PushAsync(new ReportIssuePage(_authSession.CurrentUser));
            });
        }

        public ObservableCollection<IssueReport> Reports { get; } = new();

        public ICommand GoToReportCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        // Exposed so the page's OnAppearing can decide whether to present the login modal
        // (no one signed in yet) or refresh the list (already signed in).
        public User? CurrentUser => _authSession.CurrentUser;

        // Called from the page's OnAppearing — fires once at app start (skipped, since
        // CurrentUser is still null then) and again once PopModalAsync reveals this page
        // after a successful login, or after returning from ReportIssuePage.
        public async Task LoadReportsAsync()
        {
            if (_authSession.CurrentUser is null)
                return;

            IsLoading = true;
            try
            {
                var reports = await _databaseService.GetIssueReportsByUserAsync(_authSession.CurrentUser.Id);

                Reports.Clear();
                foreach (var report in reports)
                    Reports.Add(report);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
