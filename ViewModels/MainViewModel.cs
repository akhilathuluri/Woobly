using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Woobly.Models;
using Woobly.Services;

namespace Woobly.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly WeatherService _weatherService;
        private readonly AIService _aiService;
        private readonly MediaService _mediaService;
        private readonly ClipboardService _clipboardService;
        private readonly StorageService _storageService;
        private readonly DispatcherTimer _updateTimer;
        
        private bool _isExpanded;
        private int _currentPageIndex;
        private SystemInfo _systemInfo = new SystemInfo();
        private MediaInfo _mediaInfo = new MediaInfo();
        private string _aiResponse = string.Empty;
        private ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        private ObservableCollection<ClipboardItem> _clipboardItems = new ObservableCollection<ClipboardItem>();
        private AppSettings _settings = new AppSettings();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public int CurrentPageIndex
        {
            get => _currentPageIndex;
            set { _currentPageIndex = value; OnPropertyChanged(); }
        }

        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set { _systemInfo = value; OnPropertyChanged(); }
        }

        public MediaInfo MediaInfo
        {
            get => _mediaInfo;
            set { _mediaInfo = value; OnPropertyChanged(); }
        }

        public string AIResponse
        {
            get => _aiResponse;
            set { _aiResponse = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set { _tasks = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ClipboardItem> ClipboardItems
        {
            get => _clipboardItems;
            set { _clipboardItems = value; OnPropertyChanged(); }
        }

        public AppSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _systemMonitor = new SystemMonitorService();
            _weatherService = new WeatherService();
            _aiService = new AIService();
            _mediaService = new MediaService();
            _clipboardService = new ClipboardService();
            _storageService = new StorageService();

            // Load settings and tasks
            Settings = _storageService.LoadSettings();
            Tasks = new ObservableCollection<TaskItem>(_storageService.LoadTasks());
            ClipboardItems = new ObservableCollection<ClipboardItem>();

            // Initial system info
            SystemInfo = _systemMonitor.GetSystemInfo();

            // Set up update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // Subscribe to events
            _mediaService.MediaChanged += media => MediaInfo = media;
            _clipboardService.ClipboardChanged += items => 
            {
                ClipboardItems.Clear();
                foreach (var item in items)
                {
                    ClipboardItems.Add(item);
                }
            };

            // Initial weather update
            _ = UpdateWeather();
        }

        private async void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            _systemMonitor.UpdateSystemInfo(SystemInfo);
            OnPropertyChanged(nameof(SystemInfo));
            
            // Update weather every 5 minutes
            if (DateTime.Now.Second == 0 && DateTime.Now.Minute % 5 == 0)
            {
                await UpdateWeather();
            }
        }

        private async System.Threading.Tasks.Task UpdateWeather()
        {
            if (!string.IsNullOrWhiteSpace(Settings.OpenWeatherApiKey))
            {
                var (temp, condition, icon) = await _weatherService.GetWeatherAsync(
                    Settings.OpenWeatherApiKey, 
                    Settings.City
                );
                
                SystemInfo.Temperature = temp;
                SystemInfo.WeatherCondition = condition;
                SystemInfo.WeatherIcon = icon;
                OnPropertyChanged(nameof(SystemInfo));
            }
        }

        public async void SendAIMessage(string message)
        {
            AIResponse = "Thinking...";
            var response = await _aiService.GetResponseAsync(
                Settings.OpenRouterApiKey ?? string.Empty, 
                Settings.OpenRouterModel, 
                message
            );
            AIResponse = response;
        }

        public void AddTask(string content)
        {
            var task = new TaskItem { Content = content };
            Tasks.Add(task);
            _storageService.SaveTasks(Tasks);
        }

        public void SaveTasks()
        {
            _storageService.SaveTasks(Tasks);
        }

        public void RemoveTask(Guid taskId)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                Tasks.Remove(task);
                _storageService.SaveTasks(Tasks);
            }
        }

        public void RestoreClipboard(string text)
        {
            _clipboardService.RestoreToClipboard(text);
        }

        public void SaveSettings()
        {
            _storageService.SaveSettings(Settings);
            
            // Immediately update weather with new settings
            _ = UpdateWeather();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
