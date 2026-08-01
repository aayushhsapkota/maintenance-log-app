using System.Collections.ObjectModel;
using Fix_It.Models;
using Fix_It.Services;

namespace Fix_It.ViewModels
{
    // Backs NotificationsPage — a read-only history of fired notifications (currently just
    // "New Issue Reported"), grouped by Today/Yesterday/Earlier. No read/unread tracking or
    // "Mark all read" — there's no per-user state for that, and only one notification type
    // exists right now.
    public class NotificationsViewModel : BaseViewModel
    {
        bool _isLoading;

        public ObservableCollection<NotificationLogEntry> TodayNotifications { get; } = new();
        public ObservableCollection<NotificationLogEntry> YesterdayNotifications { get; } = new();
        public ObservableCollection<NotificationLogEntry> EarlierNotifications { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool HasToday => TodayNotifications.Count > 0;
        public bool HasYesterday => YesterdayNotifications.Count > 0;
        public bool HasEarlier => EarlierNotifications.Count > 0;
        public bool HasNone => !HasToday && !HasYesterday && !HasEarlier;

        public async Task LoadNotificationsAsync()
        {
            IsLoading = true;
            try
            {
                var entries = await FirebaseDataManager.GetRecentNotificationsAsync();

                TodayNotifications.Clear();
                YesterdayNotifications.Clear();
                EarlierNotifications.Clear();

                var today = DateTime.Now.Date;
                var yesterday = today.AddDays(-1);

                foreach (var entry in entries)
                {
                    var localDate = entry.TimestampUtc.ToLocalTime().Date;
                    if (localDate == today)
                        TodayNotifications.Add(entry);
                    else if (localDate == yesterday)
                        YesterdayNotifications.Add(entry);
                    else
                        EarlierNotifications.Add(entry);
                }

                OnPropertyChanged(nameof(HasToday));
                OnPropertyChanged(nameof(HasYesterday));
                OnPropertyChanged(nameof(HasEarlier));
                OnPropertyChanged(nameof(HasNone));
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
