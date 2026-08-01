using CommunityToolkit.WinUI.Notifications;

// Namespace has to match Services/NotificationManager.cs exactly — the folder-default
// namespace here would be Fix_It.Platforms.Windows, which is NOT the same partial class.
namespace Fix_It.Services
{
    public static partial class NotificationManager
    {
        static partial void DoSendNotification(string title, string message, DateTime scheduledTime)
        {
            var button = new ToastButton()
                .SetContent("View")
                .AddArgument("action", "viewReport")
                .SetAfterActivationBehavior(ToastAfterActivationBehavior.Default);

            new ToastContentBuilder()
                .AddArgument("action", "openApp")
                .AddText(title)
                .AddText(message)
                .AddButton(button)
                .Schedule(scheduledTime);
        }
    }
}
