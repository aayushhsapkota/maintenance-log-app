using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Fix_It.Services
{
    // Matches the Week 9 lab's NotificationManager structure: a static partial class so
    // push-notification code (scheduled system notifications, platform-specific per
    // Windows/Android) can be added onto this same class later without touching ShowToast.
    public static partial class NotificationManager
    {
        // Show a toast popup with the specified message
        public static async void ShowToast(string message)
        {
            try
            {
                var cancellationTokenSource = new CancellationTokenSource();

                // Specify the duration and font size
                var duration = ToastDuration.Short;
                double fontSize = 14;

                var toast = Toast.Make(message, duration, fontSize);

                await toast.Show(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                // A toast failing to show is a UI nicety, not something that should be able to
                // crash the app — same defensive pattern used for the MediaPicker calls elsewhere
                // (both are called from fire-and-forget contexts where an unhandled exception
                // would otherwise propagate and take the whole app down with it).
                Console.WriteLine($"ShowToast failed: {ex.Message}");
            }
        }

        // Publicly available caller for DoSendNotification (partial methods can't be public).
        // Callers here always pass DateTime.Now — we're reacting to a change just detected via
        // polling (see IssueListViewModel) rather than scheduling something genuinely in the
        // future — but scheduledTime is a real parameter, so a future "remind me later" feature
        // could reuse this unchanged.
        public static void SendNotification(string title, string message, DateTime scheduledTime)
        {
            DoSendNotification(title, message, scheduledTime);
        }

        // Partial function signature to implement in platform-specific code
        // (Platforms/Windows/NotificationManager.cs, Platforms/Android/NotificationManager.cs).
        static partial void DoSendNotification(string title, string message, DateTime scheduledTime);
    }
}
