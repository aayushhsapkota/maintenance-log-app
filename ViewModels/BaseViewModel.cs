using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fix_It.ViewModels
{
    // Every ViewModel inherits from this for INotifyPropertyChanged, which is what lets
    // XAML bindings know to refresh the UI when a property changes in code.
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // [CallerMemberName] fills in propertyName automatically, so callers just write
        // SetProperty(ref _username, value) instead of raising PropertyChanged by hand.
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // For computed properties that need to notify the UI without a backing field being set.
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
