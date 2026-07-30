using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;

namespace Fix_It.ViewModels
{
    // Backs ReportIssuePage.
    public class ReportIssueViewModel : BaseViewModel
    {
        readonly Page _page;
        readonly string _createdByFirebaseUid;
        readonly DeviceInfoHelper _deviceInfoHelper = new();

        byte[]? _photoBytes;

        string _title = string.Empty;
        string _location = string.Empty;
        string _description = string.Empty;
        string? _selectedPriority;
        ImageSource? _photoPreview;
        string _photoStatusText = string.Empty;
        string _errorMessage = string.Empty;
        bool _isBusy;

        // Takes the Page itself (rather than just INavigation) since submitting also needs
        // to show a confirmation alert, which lives on Page alongside Navigation.
        public ReportIssueViewModel(Page page, string createdByFirebaseUid)
        {
            _page = page;
            _createdByFirebaseUid = createdByFirebaseUid;

            SubmitCommand = new Command(async () => await SubmitAsync());
            TakePhotoCommand = new Command(async () => await TakePhotoAsync());
            PickPhotoCommand = new Command(async () => await PickPhotoAsync());
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

        // Built from the in-memory photo bytes for the preview Image — nothing is written to
        // local disk anymore, the photo only ever leaves memory via the upload to Storage.
        public ImageSource? PhotoPreview
        {
            get => _photoPreview;
            private set => SetProperty(ref _photoPreview, value);
        }

        public bool HasPhoto => _photoBytes is not null;

        public string PhotoStatusText
        {
            get => _photoStatusText;
            private set => SetProperty(ref _photoStatusText, value);
        }

        // MediaPicker's capture support is Android/iOS only — stock MAUI has no Windows camera
        // capture, so the Camera button binds its IsEnabled to this instead of throwing at runtime.
        public bool IsCameraSupported => MediaPicker.Default.IsCaptureSupported;

        public ICommand SubmitCommand { get; }
        public ICommand TakePhotoCommand { get; }
        public ICommand PickPhotoCommand { get; }

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
                    CreatedByFirebaseUid = _createdByFirebaseUid,
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Uploads the photo to Firebase Storage (if any) and writes the report to
                // Firestore in one call — see FirebaseDataManager.SaveIssueReportAsync.
                var success = await FirebaseDataManager.SaveIssueReportAsync(report, _photoBytes);
                if (!success)
                {
                    ErrorMessage = "Failed to submit report. Please check your connection and try again.";
                    return;
                }

                await _page.DisplayAlertAsync("Report Submitted", "Your maintenance issue has been reported.", "OK");

                await _page.Navigation.PopAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        async Task TakePhotoAsync()
        {
            ErrorMessage = string.Empty;

            // Declaring the permission in the platform manifest (done back in Step 1) only gets
            // us so far — this is the runtime "request via popup" half of the requirement.
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                ErrorMessage = "Camera permission is required to take a photo.";
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
                return; // user cancelled

            await LoadPhotoAsync(photo);
            PhotoStatusText = $"Photo captured ({_deviceInfoHelper.GetCaptureSourceLabel()})";
        }

        async Task PickPhotoAsync()
        {
            ErrorMessage = string.Empty;

            // PickPhotoAsync is obsolete in favor of PickPhotosAsync (supports multi-select);
            // we only want one image for this form, so just take the first result.
            var photos = await MediaPicker.Default.PickPhotosAsync();
            var photo = photos.FirstOrDefault();
            if (photo is null)
                return; // user cancelled

            await LoadPhotoAsync(photo);
            PhotoStatusText = "Photo selected";
        }

        // Reads whatever MediaPicker handed back straight into memory — no local file write.
        // The bytes stay in _photoBytes until Submit uploads them to Firebase Storage.
        async Task LoadPhotoAsync(FileResult photo)
        {
            using var stream = await photo.OpenReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            _photoBytes = buffer.ToArray();

            // ImageSource.FromStream takes a factory rather than a stream directly because the
            // Image control may need to re-read it; a fresh MemoryStream per call handles that.
            var bytes = _photoBytes;
            PhotoPreview = ImageSource.FromStream(() => new MemoryStream(bytes));
            OnPropertyChanged(nameof(HasPhoto));
        }
    }
}
