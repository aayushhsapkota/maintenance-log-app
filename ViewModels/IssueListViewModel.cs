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

        // Tracks "have we ever completed a load" separately from the dictionary above — using
        // _lastKnownStatusByReportId.Count == 0 for that check was a bug: if the very first load
        // happens to find zero existing reports (e.g. a fresh Firestore collection, or your very
        // first report ever), the baseline stays empty after that load too, so Count == 0 never
        // stops being true and every future report looks like "still the first load" forever —
        // silently skipping the notification and Firestore log write every single time.
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

        // Dashboard stat cards — Open/Resolved only, no "In Progress" card, since that status
        // doesn't exist without a staff-assignment workflow we deliberately didn't build.
        // Computed from Reports rather than tracked separately, so they can't drift out of sync
        // with the list itself; re-notified manually in LoadReportsAsync since a filtered count
        // isn't something data binding recalculates on its own.
        public int OpenCount => Reports.Count(r => r.IsOpen);
        public int ResolvedCount => Reports.Count(r => r.IsResolved);

        public ICommand GoToReportCommand { get; }
        public ICommand GoToDetailCommand { get; }
        public ICommand GoToNotificationsCommand { get; }

        bool _hasUnseenNotifications;

        // Drives the red dot badge on the bell icon — a lightweight "something's new" signal,
        // not per-item read/unread tracking. Set when a notification fires, cleared as soon as
        // the Notifications screen is opened.
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

                // Figure out what's new against the CURRENT baseline before replacing it, so a
                // rapid second call (e.g. the poll timer) can't race with the fire-and-forget
                // notification step below.
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

                // Deliberately not awaited: the permission prompt and system-notification calls
                // must never block the visible refresh above (that's exactly what was happening
                // before — the whole screen sat frozen until you answered the OS permission
                // dialog).
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
                    // Every signed-in user's device independently notices this and notifies
                    // itself — the closest approximation of "notify all staff" without a server.
                    NotificationManager.SendNotification("New Issue Reported", report.Title, DateTime.Now.AddSeconds(1));

                    // Logged separately from the system notification so the Notifications tab
                    // has history to show even after the OS tray notification is dismissed.
                    await FirebaseDataManager.LogNotificationAsync("New Issue Reported", report.Title);

                    HasUnseenNotifications = true;
                }
            }
            catch (Exception ex)
            {
                // Running fire-and-forget (see the call site above) — an unhandled exception
                // here would otherwise be unobserved and could crash the app outright instead of
                // just failing this one background notification pass.
                Console.WriteLine($"NotifyAboutChangesAsync failed: {ex.Message}");
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
