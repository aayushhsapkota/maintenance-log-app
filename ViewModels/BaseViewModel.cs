using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Alerts;

namespace Fix_It.ViewModels
{
    // Every ViewModel in the app inherits from this. It implements INotifyPropertyChanged,
    // which is what lets XAML bindings (Text="{Binding Username}") know to refresh the UI
    // whenever a property's value changes in code.
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // Shared by any ViewModel that wants a brief, auto-dismissing confirmation (e.g. "Login
        // successful!") instead of a DisplayAlert that needs a tap to dismiss. Wrapped in its
        // own try/catch — like the MediaPicker commands elsewhere in the app, this is called
        // from Command lambdas that run fire-and-forget, so an unhandled exception here would
        // otherwise crash the whole app over what's just a UI nicety.
        protected static async Task ShowToastAsync(string message)
        {
            try
            {
                await Toast.Make(message).Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowToastAsync failed: {ex.Message}");
            }
        }

        // Call this from a property setter instead of raising PropertyChanged by hand everywhere.
        // [CallerMemberName] automatically fills in propertyName with the caller's property name,
        // so callers just write: SetProperty(ref _username, value);
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // Check if the value actually changed. If not; return
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // For read-only/computed properties (e.g. "HeadlineText" derived from "IsRegisterMode")
        // that need to notify the UI even though nothing set their backing field directly.
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
