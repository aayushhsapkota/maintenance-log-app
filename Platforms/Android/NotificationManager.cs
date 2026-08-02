using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Fix_It;

// Namespace has to match Services/NotificationManager.cs exactly — the folder-default
// namespace here would be Fix_It.Platforms.Android, which is NOT the same partial class.
namespace Fix_It.Services
{
    public static partial class NotificationManager
    {
        static readonly Context Context;
        public const string ChannelId = "1";

        // Runs once, before the first call to DoSendNotification — gets the Android
        // NotificationManager for this device and creates the channel notifications are sent on.
        static NotificationManager()
        {
            Context = Platform.CurrentActivity!.ApplicationContext!;

            var channelName = "Fix It Notifications";
            var channel = new NotificationChannel(ChannelId, channelName, NotificationImportance.Default);

            var notificationManager = Context.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
            notificationManager!.CreateNotificationChannel(channel);
        }

        // Fires the notification directly rather than scheduling it via AlarmManager (which is
        // what this used to do, via an AlarmReceiver BroadcastReceiver). Every call here is
        // really "right now" — we're reacting to something just detected via polling, not
        // setting a genuine future reminder — and AlarmManager's inexact-alarm battery/Doze-mode
        // heuristics (its whole point is tolerating scheduling slop in exchange for battery
        // savings) were causing inconsistent, sometimes-missing delivery for exactly that
        // reason. scheduledTime goes unused now, but stays in the shared signature (see
        // Services/NotificationManager.cs) in case a real "remind me later" feature reuses it.
        static partial void DoSendNotification(string title, string message, DateTime scheduledTime)
        {
            // Launch the app when the notification is tapped.
            var resultIntent = new Intent(Context, typeof(MainActivity));
            resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

            const int pendingIntentId = 0;
            // Bound as PendingIntent? since the Java API can technically return null (e.g. with
            // the NoCreate flag), which we're not using here, so this is never actually null.
            var pendingIntent = PendingIntent.GetActivity(Context, pendingIntentId, resultIntent, PendingIntentFlags.Immutable)!;

            // The Android binding for NotificationCompat.Builder's fluent Set* methods marks
            // their return type nullable even though the underlying Java API always returns
            // `this` — the warnings below are binding-generator noise, not a real null risk.
#pragma warning disable CS8602
            var builder = new NotificationCompat.Builder(Context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate))
                // The generated app icon — this project doesn't have a dedicated
                // notification-only drawable.
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
