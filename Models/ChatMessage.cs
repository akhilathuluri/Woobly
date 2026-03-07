using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Woobly.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content = string.Empty;

        public string Role { get; set; } = "user";

        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

        public bool IsUser => Role == "user";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
