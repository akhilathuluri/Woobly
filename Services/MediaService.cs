using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Control;
using System.Windows.Threading;
using Woobly.Models;

namespace Woobly.Services
{
    public class MediaService
    {
        private DispatcherTimer _updateTimer;
        private MediaInfo _currentMedia;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

        public event Action<MediaInfo>? MediaChanged;

        public MediaService()
        {
            _currentMedia = new MediaInfo { IsAvailable = false };
            
            InitializeMediaControlAsync();
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (s, e) => await UpdateMediaInfoAsync();
            _updateTimer.Start();
        }

        private async void InitializeMediaControlAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch { }
        }

        private async Task UpdateMediaInfoAsync()
        {
            try
            {
                if (_sessionManager == null)
                    return;

                var currentSession = _sessionManager.GetCurrentSession();
                if (currentSession == null)
                {
                    _currentMedia.IsAvailable = false;
                    MediaChanged?.Invoke(_currentMedia);
                    return;
                }

                var playbackInfo = currentSession.GetPlaybackInfo();
                var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();

                if (mediaProperties != null)
                {
                    _currentMedia.IsAvailable = true;
                    _currentMedia.Title = mediaProperties.Title ?? "Unknown";
                    _currentMedia.Artist = mediaProperties.Artist ?? "Unknown Artist";
                    _currentMedia.Album = mediaProperties.AlbumTitle ?? string.Empty;
                    
                    var timeline = currentSession.GetTimelineProperties();
                    _currentMedia.Duration = timeline.EndTime;
                    _currentMedia.Position = timeline.Position;
                    
                    _currentMedia.IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                }
                else
                {
                    _currentMedia.IsAvailable = false;
                }

                MediaChanged?.Invoke(_currentMedia);
            }
            catch
            {
                _currentMedia.IsAvailable = false;
                MediaChanged?.Invoke(_currentMedia);
            }
        }

        public MediaInfo GetCurrentMedia()
        {
            return _currentMedia;
        }
    }
}
