using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueDetailPage. Any signed-in user can resolve an issue (no role-based accounts,
    // so a confirmation popup stands in for a real permission check); only the creator can
    // edit, and never after it's resolved.
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

        // Called from OnAppearing so returning from a successful edit shows the saved changes.
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

            // Stand-in for a real maintenance-staff permission check.
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

                // Report was mutated in place, so SetProperty's equality check won't catch
                // the change — raise these directly.
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
