using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Fix_It.Services
{
    // Static partial class so platform-specific notification code (Windows/Android) can
    // live alongside ShowToast without touching it.
    public static partial class NotificationManager
    {
        public static async void ShowToast(string message)
        {
            try
            {
                var cancellationTokenSource = new CancellationTokenSource();
                var duration = ToastDuration.Short;
                double fontSize = 14;

                var toast = Toast.Make(message, duration, fontSize);

                await toast.Show(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                // A toast failing to show shouldn't be able to crash the app.
                Console.WriteLine($"ShowToast failed: {ex.Message}");
            }
        }

        // Public caller for DoSendNotification, since partial methods can't be public.
        public static void SendNotification(string title, string message, DateTime scheduledTime)
        {
            DoSendNotification(title, message, scheduledTime);
        }

        // Implemented per-platform in Platforms/Windows and Platforms/Android.
        static partial void DoSendNotification(string title, string message, DateTime scheduledTime);
    }
}
