using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueListPage, the app's root page. Lists every issue, not just the current
    // user's — anyone can resolve any issue since there's no role-based accounts yet.
    public class IssueListViewModel : BaseViewModel
    {
        // Stands in for real push notifications — see LoadReportsAsync and StartPolling.
        static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);

        readonly Page _page;
        readonly AuthSession _authSession;

        bool _isLoading;
        bool _isSignedIn;
        IDispatcherTimer? _pollTimer;

        // reportId -> status as of the last load, used as the baseline for the next diff.
        Dictionary<string, string> _lastKnownStatusByReportId = new();

        // Tracks whether a load has ever completed. Using the dictionary's Count == 0 for this
        // was a bug — a genuinely empty first load leaves it stuck looking like "first load"
        // forever, silently skipping notifications every time.
        bool _hasLoadedBaseline;

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

            GoToNotificationsCommand = new Command(async () =>
            {
                await _page.Navigation.PushAsync(new NotificationsPage());
                HasUnseenNotifications = false;
            });
        }

        public ObservableCollection<IssueReport> Reports { get; } = new();

        // Computed from Reports so they can't drift out of sync; re-raised manually in
        // LoadReportsAsync since data binding won't recalculate a filtered count on its own.
        public int OpenCount => Reports.Count(r => r.IsOpen);
        public int ResolvedCount => Reports.Count(r => r.IsResolved);

        public ICommand GoToReportCommand { get; }
        public ICommand GoToDetailCommand { get; }
        public ICommand GoToNotificationsCommand { get; }

        bool _hasUnseenNotifications;

        // Drives the red dot on the bell icon. Set when a notification fires, cleared on open.
        public bool HasUnseenNotifications
        {
            get => _hasUnseenNotifications;
            private set => SetProperty(ref _hasUnseenNotifications, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        // Shows a loading placeholder for the brief window before the login modal appears,
        // instead of flashing the real content first.
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

        // Lets OnAppearing decide whether to show the login modal or refresh the list.
        public User? CurrentUser => _authSession.CurrentUser;

        // Called from OnAppearing (after login, after returning from another page) and from
        // the poll timer. Always diffs against the previous snapshot, not just on the timer
        // tick, so returning right after creating a report still triggers a notification for it.
        public async Task LoadReportsAsync()
        {
            if (_authSession.CurrentUser is null)
                return;

            IsSignedIn = true;
            IsLoading = true;
            try
            {
                var reports = await FirebaseDataManager.GetAllIssueReportsAsync();

                // Diff against the current baseline before replacing it, to avoid racing a second call.
                var newReports = _hasLoadedBaseline
                    ? reports.Where(r => !_lastKnownStatusByReportId.ContainsKey(r.Id)).ToList()
                    : new List<IssueReport>();

                _lastKnownStatusByReportId = reports.ToDictionary(r => r.Id, r => r.Status);
                _hasLoadedBaseline = true;

                Reports.Clear();
                foreach (var report in reports)
                    Reports.Add(report);

                OnPropertyChanged(nameof(OpenCount));
                OnPropertyChanged(nameof(ResolvedCount));

                // Not awaited — the permission prompt shouldn't block the visible refresh above.
                if (newReports.Count > 0)
                    _ = NotifyAboutChangesAsync(newReports);
            }
            finally
            {
                IsLoading = false;
            }
        }

        async Task NotifyAboutChangesAsync(List<IssueReport> newReports)
        {
            try
            {
                // Android 13+ requires this runtime permission before any notification can
                // actually show — silently a no-op on other platforms.
                await new PostNotificationsPermission().RequestAsync();

                foreach (var report in newReports)
                {
                    // Every signed-in user's device notices this independently and notifies itself.
                    NotificationManager.SendNotification("New Issue Reported", report.Title, DateTime.Now.AddSeconds(1));

                    // Logged separately so the Notifications tab keeps history after the OS
                    // tray notification is dismissed.
                    await FirebaseDataManager.LogNotificationAsync("New Issue Reported", report.Title);

                    HasUnseenNotifications = true;
                }
            }
            catch (Exception ex)
            {
                // Fire-and-forget, so catch here instead of letting an unobserved exception crash the app.
                Console.WriteLine($"NotifyAboutChangesAsync failed: {ex.Message}");
            }
        }

        // Started/stopped from OnAppearing/OnDisappearing — only poll while this page is visible.
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
