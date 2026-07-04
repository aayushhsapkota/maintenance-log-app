using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fix_It.ViewModels
{
    // Every ViewModel in the app inherits from this. It implements INotifyPropertyChanged,
    // which is what lets XAML bindings (Text="{Binding Username}") know to refresh the UI
    // whenever a property's value changes in code.
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

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
