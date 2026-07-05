using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;

namespace Fix_It.ViewModels
{
    // Backs ReportIssuePage. Photo capture/upload is intentionally left out for now —
    // PhotoPath stays null until that's added; everything else in IssueReport is saved as normal.
    public class ReportIssueViewModel : BaseViewModel
    {
        readonly DatabaseService _databaseService;
        readonly Page _page;
        readonly int _createdByUserId;

        string _title = string.Empty;
        string _location = string.Empty;
        string _description = string.Empty;
        string? _selectedPriority;
        string _errorMessage = string.Empty;
        bool _isBusy;

        // Takes the Page itself (rather than just INavigation) since submitting also needs
        // to show a confirmation alert, which lives on Page alongside Navigation.
        public ReportIssueViewModel(DatabaseService databaseService, Page page, int createdByUserId)
        {
            _databaseService = databaseService;
            _page = page;
            _createdByUserId = createdByUserId;

            SubmitCommand = new Command(async () => await SubmitAsync());
        }

        // Bound to the Priority Picker's ItemsSource — a fixed list is enough for this phase,
        // no need for a database table just to hold four strings.
        public ObservableCollection<string> Priorities { get; } = new()
        {
            "Low", "Medium", "High", "Urgent"
        };

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string? SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public ICommand SubmitCommand { get; }

        async Task SubmitAsync()
        {
            if (_isBusy)
                return;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Location) ||
                string.IsNullOrWhiteSpace(Description) || string.IsNullOrWhiteSpace(SelectedPriority))
            {
                ErrorMessage = "Please fill in all required fields.";
                return;
            }

            _isBusy = true;
            try
            {
                var report = new IssueReport
                {
                    Title = Title,
                    Location = Location,
                    Description = Description,
                    Priority = SelectedPriority,
                    PhotoPath = null,
                    CreatedByUserId = _createdByUserId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _databaseService.SaveIssueReportAsync(report);

                await _page.DisplayAlertAsync("Report Submitted", "Your maintenance issue has been reported.", "OK");

                await _page.Navigation.PopAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
