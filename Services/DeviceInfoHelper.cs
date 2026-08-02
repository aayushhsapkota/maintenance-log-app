namespace Fix_It.Services
{
    // Shared declaration — each platform folder under Platforms/ supplies its own
    // implementation of GetCaptureSourceLabel(), used to label which platform a photo came from.
    public partial class DeviceInfoHelper
    {
        public partial string GetCaptureSourceLabel();
    }
}
