namespace Fix_It.Services
{
    // The shared declaration lives here; each platform folder under Platforms/ supplies its own
    // implementation of GetCaptureSourceLabel(). Only the implementation matching the TFM being
    // built gets compiled in, so this is the concrete example of "partial classes for
    // platform-specific code" — used here just to label which platform a photo came from.
    public partial class DeviceInfoHelper
    {
        public partial string GetCaptureSourceLabel();
    }
}
