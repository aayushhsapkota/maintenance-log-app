using Android.App;
using Android.Content;
using Android.Runtime;
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

        static partial void DoSendNotification(string title, string message, DateTime scheduledTime)
        {
            var alarmManager = Context.GetSystemService(Android.Content.Context.AlarmService).JavaCast<AlarmManager>();

            int id = new Random().Next(int.MaxValue);
            var alarmIntent = new Intent(Context, typeof(AlarmReceiver));

            // Extra tags so AlarmReceiver can read them back when the alarm fires.
            alarmIntent.PutExtra("id", id);
            alarmIntent.PutExtra("title", title);
            alarmIntent.PutExtra("message", message);

            var dateOffsetValue = new DateTimeOffset(scheduledTime);
            long millisecondsToBegin = dateOffsetValue.ToUnixTimeMilliseconds();

            // Bound as PendingIntent? since the Java API can technically return null (e.g. with
            // the NoCreate flag), which we're not using here, so this is never actually null.
            var pending = PendingIntent.GetBroadcast(Context, id, alarmIntent, PendingIntentFlags.Immutable)!;

            // Schedule the alarm to trigger AlarmReceiver at the designated time.
            alarmManager!.Set(AlarmType.RtcWakeup, millisecondsToBegin, pending);
        }
    }

    [BroadcastReceiver]
    public class AlarmReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context is null || intent is null)
                return;

            var title = intent.GetStringExtra("title") ?? string.Empty;
            var message = intent.GetStringExtra("message") ?? string.Empty;
            var id = intent.GetIntExtra("id", 0);

            // Launch the app when the notification is tapped.
            var resultIntent = new Intent(context, typeof(MainActivity));
            resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

            const int pendingIntentId = 0;
            var pendingIntent = PendingIntent.GetActivity(context, pendingIntentId, resultIntent, PendingIntentFlags.Immutable);

            // The Android binding for NotificationCompat.Builder's fluent Set* methods marks
            // their return type nullable even though the underlying Java API always returns
            // `this` — the warnings below are binding-generator noise, not a real null risk.
#pragma warning disable CS8602
            var builder = new NotificationCompat.Builder(context, NotificationManager.ChannelId)
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

            var notificationManager = context.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;

            notificationManager!.Notify(id, notification);
        }
    }
}
