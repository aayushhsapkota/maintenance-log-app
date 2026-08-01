using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueListPage — the app's root page. Lists every submitted issue, not just the
    // signed-in user's own — anyone can resolve any issue (no role-based accounts), which only
    // makes sense if everyone can actually see issues they didn't create in the first place.
    public class IssueListViewModel : BaseViewModel
    {
        // Polling interval for the foreground push-notification approximation — see
        // LoadReportsAsync and StartPolling. Short enough to demo without a long wait; there's
        // no server pushing to us, so this is what stands in for it.
        static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);

        readonly Page _page;
        readonly AuthSession _authSession;

        bool _isLoading;
        bool _isSignedIn;
        IDispatcherTimer? _pollTimer;

        // reportId -> status, as of the last LoadReportsAsync call — the baseline the next
        // call's diff compares against.
        Dictionary<string, string> _lastKnownStatusByReportId = new();

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

            GoToDetailCommand = new Command<IssueReport>(async report =>
            {
                if (report is not null)
                    await _page.Navigation.PushAsync(new IssueDetailPage(report));
            });
        }

        public ObservableCollection<IssueReport> Reports { get; } = new();

        public ICommand GoToReportCommand { get; }
        public ICommand GoToDetailCommand { get; }

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
        // CurrentUser is still null then), again once PopModalAsync reveals this page after a
        // successful login, again on returning from ReportIssuePage/IssueDetailPage, and
        // periodically from the polling timer (see StartPolling).
        //
        // Always diffs against the previous snapshot (NotifyAboutChanges) rather than only doing
        // so from the timer — diffing is a no-op when nothing actually changed, so there's no
        // real downside, and it's what makes "return to the list right after creating or
        // resolving something yourself" actually fire a notification instead of that change
        // getting silently folded into the baseline before the next poll ever sees it.
        public async Task LoadReportsAsync()
        {
            if (_authSession.CurrentUser is null)
                return;

            IsSignedIn = true;
            IsLoading = true;
            try
            {
                var reports = await FirebaseDataManager.GetAllIssueReportsAsync();

                NotifyAboutChanges(reports);
                _lastKnownStatusByReportId = reports.ToDictionary(r => r.Id, r => r.Status);

                Reports.Clear();
                foreach (var report in reports)
                    Reports.Add(report);
            }
            finally
            {
                IsLoading = false;
            }
        }

        void NotifyAboutChanges(List<IssueReport> reports)
        {
            // Nothing to compare against yet (first load this session) — skip rather than
            // treating every existing report as "new".
            if (_lastKnownStatusByReportId.Count == 0)
                return;

            var currentUserUid = _authSession.CurrentUser?.FirebaseUid;

            foreach (var report in reports)
            {
                if (!_lastKnownStatusByReportId.TryGetValue(report.Id, out var previousStatus))
                {
                    // Every signed-in user's device independently notices this and notifies
                    // itself — the closest approximation of "notify all staff" without a server.
                    NotificationManager.SendNotification("New Issue Reported", report.Title, DateTime.Now);
                }
                else if (previousStatus == "Open" && report.IsResolved && report.CreatedByFirebaseUid == currentUserUid)
                {
                    // Only fires on the reporter's own device, since only their device has a
                    // report with a matching CreatedByFirebaseUid to react to.
                    NotificationManager.SendNotification("Issue Resolved", $"Your report \"{report.Title}\" has been resolved.", DateTime.Now);
                }
            }
        }

        // Started/stopped from the page's OnAppearing/OnDisappearing — polling only makes sense
        // while this page is actually the visible one.
        public void StartPolling()
        {
            if (_pollTimer is not null)
                return;

            _pollTimer = _page.Dispatcher.CreateTimer();
            _pollTimer.Interval = PollingInterval;
            _pollTimer.Tick += async (_, _) => await LoadReportsAsync();
            _pollTimer.Start();
        }

        public void StopPolling()
        {
            _pollTimer?.Stop();
            _pollTimer = null;
        }
    }
}
