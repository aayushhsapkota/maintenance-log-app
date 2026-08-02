using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Fix_It;

// Must match the namespace in Services/NotificationManager.cs, not the folder-default one.
namespace Fix_It.Services
{
    public static partial class NotificationManager
    {
        static readonly Context Context;
        public const string ChannelId = "1";

        // Runs once before the first notification — creates the notification channel.
        static NotificationManager()
        {
            Context = Platform.CurrentActivity!.ApplicationContext!;

            var channelName = "Fix It Notifications";
            var channel = new NotificationChannel(ChannelId, channelName, NotificationImportance.Default);

            var notificationManager = Context.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
            notificationManager!.CreateNotificationChannel(channel);
        }

        // Fires immediately instead of scheduling via AlarmManager — AlarmManager's inexact
        // timing caused delayed or missing notifications, since every call here means "now"
        // anyway. scheduledTime is unused but kept in the shared signature for later reuse.
        static partial void DoSendNotification(string title, string message, DateTime scheduledTime)
        {
            // Launch the app when the notification is tapped.
            var resultIntent = new Intent(Context, typeof(MainActivity));
            resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

            const int pendingIntentId = 0;
            // Never actually null here — PendingIntent? only because the Java API can return
            // null with flags we're not using.
            var pendingIntent = PendingIntent.GetActivity(Context, pendingIntentId, resultIntent, PendingIntentFlags.Immutable)!;

            // Nullable-return warnings below are binding-generator noise, not a real null risk.
#pragma warning disable CS8602
            var builder = new NotificationCompat.Builder(Context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate))
                // No dedicated notification icon, so reuse the app icon.
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentIntent(pendingIntent)
                .SetPriority((int)NotificationPriority.High);
            var notification = builder.Build();
#pragma warning restore CS8602

            var notificationManager = Context.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
            notificationManager!.Notify(new Random().Next(int.MaxValue), notification);
        }
    }
}
