using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Woobly.Models
{
    public enum CallDirection { Incoming, Outgoing, Active }

    public class CallInfo : INotifyPropertyChanged
    {
        private bool _isActive;
        private string _contactName = string.Empty;
        private string _appSource = string.Empty;
        private CallDirection _direction;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        /// <summary>Contact name extracted from the call window title.</summary>
        public string ContactName
        {
            get => _contactName;
            set { _contactName = value; OnPropertyChanged(); }
        }

        /// <summary>"WhatsApp" or "Telegram"</summary>
        public string AppSource
        {
            get => _appSource;
            set { _appSource = value; OnPropertyChanged(); }
        }

        public CallDirection Direction
        {
            get => _direction;
            set { _direction = value; OnPropertyChanged(); OnPropertyChanged(nameof(DirectionLabel)); }
        }

        public string DirectionLabel => Direction switch
        {
            CallDirection.Incoming => "Incoming",
            CallDirection.Outgoing => "Calling...",
            _ => "On Call"
        };

        /// <summary>Win32 handle of the call window — used to bring the app to focus.</summary>
        public nint WindowHandle { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
