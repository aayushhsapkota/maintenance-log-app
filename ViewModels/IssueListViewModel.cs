using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Fix_It.Views;

namespace Fix_It.ViewModels
{
    // Backs IssueListPage — a simple read-only list of the reports the signed-in employee
    // has submitted (title, location, priority, submitted date). No status workflow, stats,
    // or edit/close actions here; that's maintenance-staff territory and out of scope.
    public class IssueListViewModel : BaseViewModel
    {
        readonly DatabaseService _databaseService;
        readonly Page _page;
        readonly User _currentUser;

        bool _isLoading;

        public IssueListViewModel(DatabaseService databaseService, Page page, User currentUser)
        {
            _databaseService = databaseService;
            _page = page;
            _currentUser = currentUser;

            GoToReportCommand = new Command(async () => await _page.Navigation.PushAsync(new ReportIssuePage(_currentUser)));
        }

        public ObservableCollection<IssueReport> Reports { get; } = new();

        public ICommand GoToReportCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        // Called from the page's OnAppearing so returning from ReportIssuePage (which pops
        // back here after a submit) refreshes the list with the newly added report.
        public async Task LoadReportsAsync()
        {
            IsLoading = true;
            try
            {
                var reports = await _databaseService.GetIssueReportsByUserAsync(_currentUser.Id);

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
