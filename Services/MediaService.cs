using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Control;
using System.Windows.Threading;
using Woobly.Models;
using System.IO;
using Windows.Storage.Streams;

namespace Woobly.Services
{
    public class MediaService
    {
        private DispatcherTimer _updateTimer;
        private MediaInfo _currentMedia;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;

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

                _currentSession = _sessionManager.GetCurrentSession();
                if (_currentSession == null)
                {
                    _currentMedia.IsAvailable = false;
                    MediaChanged?.Invoke(_currentMedia);
                    return;
                }

                var playbackInfo = _currentSession.GetPlaybackInfo();
                var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();

                if (mediaProperties != null)
                {
                    _currentMedia.IsAvailable = true;
                    _currentMedia.Title = mediaProperties.Title ?? "Unknown";
                    _currentMedia.Artist = mediaProperties.Artist ?? "Unknown Artist";
                    _currentMedia.Album = mediaProperties.AlbumTitle ?? string.Empty;
                    
                    // Get album art thumbnail
                    try
                    {
                        var thumbnail = mediaProperties.Thumbnail;
                        if (thumbnail != null)
                        {
                            using var stream = await thumbnail.OpenReadAsync();
                            var reader = new DataReader(stream.GetInputStreamAt(0));
                            var bytes = new byte[stream.Size];
                            await reader.LoadAsync((uint)stream.Size);
                            reader.ReadBytes(bytes);
                            _currentMedia.AlbumArt = Convert.ToBase64String(bytes);
                        }
                        else
                        {
                            _currentMedia.AlbumArt = null;
                        }
                    }
                    catch
                    {
                        _currentMedia.AlbumArt = null;
                    }
                    
                    var timeline = _currentSession.GetTimelineProperties();
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

        // Playback control methods
        public async Task PlayPauseAsync()
        {
            try
            {
                if (_currentSession == null) return;

                if (_currentMedia.IsPlaying)
                {
                    await _currentSession.TryPauseAsync();
                }
                else
                {
                    await _currentSession.TryPlayAsync();
                }

                // Force immediate update
                await UpdateMediaInfoAsync();
            }
            catch { }
        }

        public async Task NextAsync()
        {
            try
            {
                if (_currentSession == null) return;
                await _currentSession.TrySkipNextAsync();
                await Task.Delay(500); // Wait for media change
                await UpdateMediaInfoAsync();
            }
            catch { }
        }

        public async Task PreviousAsync()
        {
            try
            {
                if (_currentSession == null) return;
                await _currentSession.TrySkipPreviousAsync();
                await Task.Delay(500); // Wait for media change
                await UpdateMediaInfoAsync();
            }
            catch { }
        }
    }
}
