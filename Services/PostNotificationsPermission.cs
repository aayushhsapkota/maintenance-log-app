namespace Fix_It.Services
{
    // Android 13+ (API 33) requires this runtime permission before the OS will show any
    // notification at all. .NET MAUI doesn't ship a built-in Permissions type for it (unlike
    // Permissions.Camera), so this follows MAUI's documented pattern for adding a custom one —
    // used as `await new PostNotificationsPermission().RequestAsync()`, not the static
    // Permissions.RequestAsync<T>() helper, since that's only wired up for MAUI's built-ins.
    // No-ops (granted) on platforms other than Android, where this requirement doesn't exist.
    public class PostNotificationsPermission : Permissions.BasePlatformPermission
    {
#if ANDROID
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new[]
        {
            ("android.permission.POST_NOTIFICATIONS", true)
        };
#endif
    }
}
