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
        readonly Page _page;
        readonly AuthSession _authSession;

        bool _isLoading;
        bool _isSignedIn;

        public IssueListViewModel(Page page, AuthSession authSession)
        {
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

        // Drives a loading placeholder in the XAML so the brief window before the login modal
        // covers this page (see App.xaml.cs) reads as an intentional loading beat instead of
        // flashing the real "My Reports" content (header, empty-state text) before login appears.
        public bool IsSignedIn
        {
            get => _isSignedIn;
            private set
            {
                if (SetProperty(ref _isSignedIn, value))
                    OnPropertyChanged(nameof(IsCheckingAuth));
            }
        }

        public bool IsCheckingAuth => !IsSignedIn;

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

            IsSignedIn = true;
            IsLoading = true;
            try
            {
                var reports = await FirebaseDataManager.GetIssueReportsByUserAsync(_authSession.CurrentUser.FirebaseUid);

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
