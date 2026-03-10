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
        private readonly StartupService _startupService;
        private readonly CallDetectionService _callDetectionService;
        private readonly BatteryNotificationService _batteryNotificationService;
        private readonly DispatcherTimer _updateTimer;
        
        private bool _isExpanded;
        private int _currentPageIndex;
        private SystemInfo _systemInfo = new SystemInfo();
        private MediaInfo _mediaInfo = new MediaInfo();
        private ObservableCollection<ChatMessage> _chatMessages = new ObservableCollection<ChatMessage>();
        private bool _isAIStreaming;
        private CallInfo _activeCall = new CallInfo();
        private ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        private ObservableCollection<ClipboardItem> _clipboardItems = new ObservableCollection<ClipboardItem>();
        private AppSettings _settings = new AppSettings();

        /// <summary>Called by the view to scroll the chat to the bottom after each token.</summary>
        public Action? RequestScrollToBottom;

        /// <summary>Called when a call is detected — view should expand the island.</summary>
        public Action? OnCallStarted;

        /// <summary>Called when a call ends — view should collapse the island.</summary>
        public Action? OnCallEnded;

        /// <summary>Called with (icon, message) when a battery event occurs — view should show a transient notification.</summary>
        public Action<string, string>? OnBatteryNotification;

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

        public ObservableCollection<ChatMessage> ChatMessages
        {
            get => _chatMessages;
            set { _chatMessages = value; OnPropertyChanged(); }
        }

        public bool IsAIStreaming
        {
            get => _isAIStreaming;
            set { _isAIStreaming = value; OnPropertyChanged(); }
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

        public CallInfo ActiveCall
        {
            get => _activeCall;
            set { _activeCall = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _systemMonitor = new SystemMonitorService();
            _weatherService = new WeatherService();
            _aiService = new AIService();
            _mediaService = new MediaService();
            _storageService = new StorageService();
            _startupService = new StartupService();
            _callDetectionService = new CallDetectionService();
            _batteryNotificationService = new BatteryNotificationService();
            _batteryNotificationService.NotificationTriggered += (icon, msg) => OnBatteryNotification?.Invoke(icon, msg);

            // Load settings and tasks
            Settings = _storageService.LoadSettings();
            Settings.RunOnStartup = _startupService.IsEnabled();
            _clipboardService = new ClipboardService(Settings.ClipboardHistoryLimit);
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
            _callDetectionService.CallStarted += call =>
            {
                ActiveCall = call;
                OnCallStarted?.Invoke();
            };
            _callDetectionService.CallEnded += () =>
            {
                ActiveCall = new CallInfo { IsActive = false };
                OnCallEnded?.Invoke();
            };
            // ContactName resolved asynchronously (e.g. via UIA for WhatsApp Desktop).
            // Since CallInfo implements INotifyPropertyChanged and ContactName has a setter,
            // simply writing to ActiveCall.ContactName updates the XAML binding live.
            _callDetectionService.ContactNameResolved += name =>
            {
                if (ActiveCall.IsActive)
                    ActiveCall.ContactName = name;
            };
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
            _batteryNotificationService.Check(SystemInfo.BatteryPercentage, SystemInfo.IsCharging);
            
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
            if (IsAIStreaming || string.IsNullOrWhiteSpace(message)) return;

            // Record user message
            ChatMessages.Add(new ChatMessage { Role = "user", Content = message });
            RequestScrollToBottom?.Invoke();

            // Placeholder for streamed assistant reply
            var assistantMsg = new ChatMessage { Role = "assistant", Content = "" };
            ChatMessages.Add(assistantMsg);
            IsAIStreaming = true;

            // Build history (everything except the empty assistant placeholder)
            var history = ChatMessages
                .Take(ChatMessages.Count - 1)
                .Select(m => (m.Role, m.Content))
                .ToList();

            var dispatcher = System.Windows.Application.Current.Dispatcher;

            await _aiService.StreamResponseAsync(
                Settings.OpenRouterApiKey,
                Settings.OpenRouterModel,
                history,
                token =>
                {
                    dispatcher.Invoke(() =>
                    {
                        assistantMsg.Content += token;
                        RequestScrollToBottom?.Invoke();
                    });
                }
            );

            // Clean up markdown from the full accumulated response
            dispatcher.Invoke(() =>
            {
                assistantMsg.Content = AIService.Sanitize(assistantMsg.Content);
            });

            IsAIStreaming = false;
        }

        public void ClearChat()
        {
            ChatMessages.Clear();
            IsAIStreaming = false;
        }

        public void BringCallWindowToFront()
        {
            _callDetectionService.BringCallWindowToFront();
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

            // Apply run-on-startup setting
            _startupService.SetEnabled(Settings.RunOnStartup);

            // Update clipboard history limit at runtime
            _clipboardService.UpdateHistoryLimit(Settings.ClipboardHistoryLimit);
            
            // Immediately update weather with new settings
            _ = UpdateWeather();
        }

        public async System.Threading.Tasks.Task MediaPlayPauseAsync()
        {
            await _mediaService.PlayPauseAsync();
        }

        public async System.Threading.Tasks.Task MediaNextAsync()
        {
            await _mediaService.NextAsync();
        }

        public async System.Threading.Tasks.Task MediaPreviousAsync()
        {
            await _mediaService.PreviousAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
