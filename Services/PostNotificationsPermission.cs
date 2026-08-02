namespace Fix_It.Services
{
    // Android 13+ requires this runtime permission before notifications can show. MAUI has
    // no built-in Permissions type for it, so this follows MAUI's pattern for a custom one.
    // No-op (granted) on other platforms.
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
