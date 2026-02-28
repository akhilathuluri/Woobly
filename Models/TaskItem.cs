using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Woobly.Models
{
    public class TaskItem : INotifyPropertyChanged
    {
        private bool _isCompleted;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Content { get; set; }
        
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
