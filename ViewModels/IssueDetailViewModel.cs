using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueDetailPage. Any signed-in user can resolve an issue — there's no role-based
    // account system, so a confirmation popup stands in for a real permission check. Only the
    // report's original creator can edit it, and once resolved nobody can edit it at all.
    public class IssueDetailViewModel : BaseViewModel
    {
        readonly Page _page;
        readonly AuthSession _authSession;

        IssueReport _report;
        bool _isBusy;

        public IssueDetailViewModel(Page page, AuthSession authSession, IssueReport report)
        {
            _page = page;
            _authSession = authSession;
            _report = report;

            EditCommand = new Command(async () =>
                await _page.Navigation.PushAsync(new ReportIssuePage(_authSession.CurrentUser!, Report)));
            ResolveCommand = new Command(async () => await ResolveAsync());
        }

        public IssueReport Report
        {
            get => _report;
            private set
            {
                if (SetProperty(ref _report, value))
                {
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(CanResolve));
                    OnPropertyChanged(nameof(IsResolved));
                }
            }
        }

        bool IsOwner => _authSession.CurrentUser?.FirebaseUid == Report.CreatedByFirebaseUid;

        public bool CanEdit => IsOwner && Report.Status != "Resolved";
        public bool CanResolve => Report.Status != "Resolved";
        public bool IsResolved => Report.Status == "Resolved";

        public ICommand EditCommand { get; }
        public ICommand ResolveCommand { get; }

        // Called from the page's OnAppearing so returning from a successful edit shows the
        // saved changes — Report is a genuinely new object here, so the normal SetProperty
        // change-detection in the setter above handles notifying the UI.
        public async Task RefreshAsync()
        {
            var refreshed = await FirebaseDataManager.GetIssueReportByIdAsync(Report.Id);
            if (refreshed is not null)
                Report = refreshed;
        }

        async Task ResolveAsync()
        {
            if (_isBusy)
                return;

            // No role-based accounts yet, so this confirmation is the stand-in for "are you
            // actually maintenance staff" rather than an enforced permission check.
            var confirmed = await _page.DisplayAlertAsync(
                "Maintenance Staff Only",
                "Only maintenance staff are allowed to resolve this issue. Are you sure you want to continue?",
                "Continue", "Cancel");
            if (!confirmed)
                return;

            _isBusy = true;
            try
            {
                var actorEmail = _authSession.CurrentUser?.Username ?? "Unknown";
                var success = await FirebaseDataManager.ResolveIssueReportAsync(Report, actorEmail);
                if (!success)
                {
                    await _page.DisplayAlertAsync("Error", "Failed to resolve the issue. Please try again.", "OK");
                    return;
                }

                // ResolveIssueReportAsync mutated Report in place (Status + Activity) rather
                // than handing back a new instance, so SetProperty's reference-equality check
                // wouldn't detect a change — raise the notifications directly instead.
                OnPropertyChanged(nameof(Report));
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanResolve));
                OnPropertyChanged(nameof(IsResolved));

                NotificationManager.ShowToast("Issue marked as resolved.");
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
