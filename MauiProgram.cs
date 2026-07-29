using Fix_It.Services;
using Microsoft.Extensions.Logging;

namespace Fix_It
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register DatabaseService as a singleton so the whole app shares one SQLite connection instance.
            builder.Services.AddSingleton<DatabaseService>();

            // Shared "who's signed in" holder — singleton so LoginPage and IssueListPage see the same instance.
            builder.Services.AddSingleton<AuthSession>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
