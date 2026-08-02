using CommunityToolkit.WinUI.Notifications;

// Must match the namespace in Services/NotificationManager.cs, not the folder-default one.
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
