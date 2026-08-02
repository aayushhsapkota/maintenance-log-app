using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Microsoft.Maui.Graphics.Platform;

namespace Fix_It.ViewModels
{
    // Backs ReportIssuePage — used for both creating a new report and, when existingReport
    // is supplied, editing one. Edit mode is only reachable via IssueDetailPage's Edit
    // button, which already guarantees the current user owns the report and it isn't resolved.
    public class ReportIssueViewModel : BaseViewModel
    {
        readonly Page _page;
        readonly User _currentUser;
        readonly IssueReport? _existingReport;
        readonly DeviceInfoHelper _deviceInfoHelper = new();

        byte[]? _photoBytes;
        readonly string? _existingPhotoUrl;

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
        public ReportIssueViewModel(Page page, User currentUser, IssueReport? existingReport = null)
        {
            _page = page;
            _currentUser = currentUser;
            _existingReport = existingReport;

            SubmitCommand = new Command(async () => await SubmitAsync());
            TakePhotoCommand = new Command(async () => await TakePhotoAsync());
            PickPhotoCommand = new Command(async () => await PickPhotoAsync());

            if (existingReport is not null)
            {
                _title = existingReport.Title;
                _location = existingReport.Location;
                _description = existingReport.Description;
                _selectedPriority = existingReport.Priority;
                _existingPhotoUrl = existingReport.PhotoUrl;

                if (existingReport.HasPhoto)
                {
                    // Loaded straight from the remote URL rather than downloaded into memory —
                    // only replaced if the user actively picks/captures a different photo.
                    PhotoPreview = ImageSource.FromUri(new Uri(existingReport.PhotoUrl!));
                    _photoStatusText = "Current photo";
                }
            }
        }

        public bool IsEditMode => _existingReport is not null;
        public string PageTitle => IsEditMode ? "Edit Issue" : "Report Issue";
        public string PageSubtitle => IsEditMode ? "Update the details below" : "New maintenance request";
        public string SubmitButtonText => IsEditMode ? "Save Changes" : "Submit Report";

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

        // Built from the in-memory photo bytes for a newly picked/captured photo, or from the
        // existing remote PhotoUrl in edit mode until the user replaces it.
        public ImageSource? PhotoPreview
        {
            get => _photoPreview;
            private set => SetProperty(ref _photoPreview, value);
        }

        public bool HasPhoto => _photoBytes is not null || !string.IsNullOrEmpty(_existingPhotoUrl);

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
                if (IsEditMode)
                {
                    var report = _existingReport!;
                    report.Title = Title;
                    report.Location = Location;
                    report.Description = Description;
                    report.Priority = SelectedPriority;

                    // _photoBytes stays null unless the user picked/captured a replacement —
                    // UpdateIssueReportAsync leaves the existing PhotoUrl alone in that case.
                    var success = await FirebaseDataManager.UpdateIssueReportAsync(report, _photoBytes, _currentUser.Username);
                    if (!success)
                    {
                        ErrorMessage = "Failed to save changes. Please check your connection and try again.";
                        return;
                    }

                    NotificationManager.ShowToast("Changes saved.");
                }
                else
                {
                    var report = new IssueReport
                    {
                        Title = Title,
                        Location = Location,
                        Description = Description,
                        Priority = SelectedPriority,
                        CreatedByFirebaseUid = _currentUser.FirebaseUid,
                        CreatedByEmail = _currentUser.Username,
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
                }

                await _page.Navigation.PopAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        // Requests the camera permission at runtime, handling two edge cases a flat
        // RequestAsync call misses: iOS won't re-prompt once denied, and Android can ask to
        // explain itself first via ShouldShowRationale.
        async Task<PermissionStatus> GetCameraPermissionAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // iOS won't show the prompt again once denied — the user must enable it in Settings.
                await _page.DisplayAlertAsync("Warning",
                    "You must manually enable camera access for this app in settings.", "OK");
                return status;
            }

            if (Permissions.ShouldShowRationale<Permissions.Camera>())
            {
                // Explain why we need it before asking a second time.
                await _page.DisplayAlertAsync("Warning",
                    "This app requires camera access to attach a photo to your report.", "OK");
            }

            status = await Permissions.RequestAsync<Permissions.Camera>();
            return status;
        }

        async Task TakePhotoAsync()
        {
            ErrorMessage = string.Empty;

            try
            {
                var status = await GetCameraPermissionAsync();
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
            catch (Exception ex)
            {
                // Commands run fire-and-forget, so catch here instead of crashing the app.
                Console.WriteLine($"TakePhotoAsync failed: {ex.Message}");
                ErrorMessage = "Couldn't open the camera. Please try again.";
            }
        }

        async Task PickPhotoAsync()
        {
            ErrorMessage = string.Empty;

            try
            {
                // PickPhotoAsync is obsolete in favor of PickPhotosAsync — take the first
                // result since this form only wants one image.
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos.FirstOrDefault();
                if (photo is null)
                    return; // user cancelled

                await LoadPhotoAsync(photo);
                PhotoStatusText = "Photo selected";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PickPhotoAsync failed: {ex.Message}");
                ErrorMessage = "Couldn't open the photo picker. Please try again.";
            }
        }

        // Full camera-resolution photos were OOM-crashing the app on memory-constrained
        // devices — capping the longest edge keeps things small without losing clarity.
        const int MaxPhotoDimension = 1600;

        async Task LoadPhotoAsync(FileResult photo)
        {
            using var sourceStream = await photo.OpenReadAsync();
            using var originalImage = PlatformImage.FromStream(sourceStream);

            // Null means either already small enough or the resize failed — fall back to the original.
            _photoBytes = TryDownscale(originalImage) ?? EncodeToJpegBytes(originalImage);

            // ImageSource.FromStream takes a factory rather than a stream directly because the
            // Image control may need to re-read it; a fresh MemoryStream per call handles that.
            var bytes = _photoBytes;
            PhotoPreview = ImageSource.FromStream(() => new MemoryStream(bytes));
            OnPropertyChanged(nameof(HasPhoto));
        }

        static byte[]? TryDownscale(Microsoft.Maui.Graphics.IImage originalImage)
        {
            if (originalImage.Width <= MaxPhotoDimension && originalImage.Height <= MaxPhotoDimension)
                return null;

            try
            {
                var scale = MaxPhotoDimension / (float)Math.Max(originalImage.Width, originalImage.Height);
                using var resized = originalImage.Resize(originalImage.Width * scale, originalImage.Height * scale, ResizeMode.Fit);
                return EncodeToJpegBytes(resized);
            }
            catch (Exception ex)
            {
                // PlatformImage's Android resize is fragile around dispose timing — fall back
                // to the original photo rather than failing the whole attach-a-photo action.
                Console.WriteLine($"Photo downscale failed, using original size: {ex.Message}");
                return null;
            }
        }

        static byte[] EncodeToJpegBytes(Microsoft.Maui.Graphics.IImage image)
        {
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Jpeg, quality: 0.8f);
            return output.ToArray();
        }
    }
}
