using System.Collections.ObjectModel;
using System.Windows.Input;
using Fix_It.Models;
using Fix_It.Services;
using Microsoft.Maui.Graphics.Platform;

namespace Fix_It.ViewModels
{
    // Backs ReportIssuePage — used both for creating a new report and, when existingReport is
    // supplied, editing one. Only reachable in edit mode via IssueDetailPage's Edit button,
    // which already guarantees the current user is the report's creator and it isn't resolved.
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

        // Declaring the permission in the platform manifest (done back in Step 1) only gets us
        // so far — this is the runtime "request via popup" half of the requirement, plus the
        // two edge cases a flat RequestAsync call glosses over: iOS won't show its permission
        // prompt a second time once denied (so we have to point the user at Settings instead),
        // and Android can be asked to explain itself first via ShouldShowRationale before the
        // user gets a second chance to grant it.
        async Task<PermissionStatus> GetCameraPermissionAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // On iOS, once a permission has been denied it may not be requested again
                // from within the app — the user has to flip it on in Settings themselves.
                await _page.DisplayAlertAsync("Warning",
                    "You must manually enable camera access for this app in settings.", "OK");
                return status;
            }

            if (Permissions.ShouldShowRationale<Permissions.Camera>())
            {
                // True if the user denied it before and it's being requested again —
                // explain why we need it before asking a second time.
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
                // Commands wrapping "async () => await ..." run fire-and-forget — an exception
                // that escapes here (e.g. no camera app on the device/emulator, or the user
                // denies the OS-level prompt) would otherwise be unhandled and crash the app
                // outright instead of just failing this one action.
                Console.WriteLine($"TakePhotoAsync failed: {ex.Message}");
                ErrorMessage = "Couldn't open the camera. Please try again.";
            }
        }

        async Task PickPhotoAsync()
        {
            ErrorMessage = string.Empty;

            try
            {
                // PickPhotoAsync is obsolete in favor of PickPhotosAsync (supports multi-select);
                // we only want one image for this form, so just take the first result.
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

        // Reads whatever MediaPicker handed back, downscales it, and keeps only the smaller
        // version in memory — no local file write. A full camera-resolution photo can be
        // several MB as raw bytes plus far more once decoded to a bitmap for the preview, which
        // was OOM-crashing the app on memory-constrained devices/emulators; capping the longest
        // edge keeps both comfortably small while still being plenty clear for a report photo.
        const int MaxPhotoDimension = 1600;

        async Task LoadPhotoAsync(FileResult photo)
        {
            using var sourceStream = await photo.OpenReadAsync();
            using var originalImage = PlatformImage.FromStream(sourceStream);

            // TryDownscale returns null if the photo's already small enough, or if the resize
            // itself failed — either way we fall back to encoding the untouched original.
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
                // PlatformImage's Android implementation has shown itself fragile around
                // resize/dispose timing ("object already disposed" JNI errors) — if it throws,
                // fall back to the original full-size photo rather than failing the whole
                // attach-a-photo action over a downscale that didn't cooperate.
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
