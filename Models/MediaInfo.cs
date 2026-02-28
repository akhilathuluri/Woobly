using System;

namespace Woobly.Models
{
    public class MediaInfo
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? AlbumArt { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Position { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsAvailable { get; set; }
        
        public string FormattedPosition => Position.ToString(@"m\:ss");
        public string FormattedDuration => Duration.ToString(@"m\:ss");
        public double ProgressPercentage => Duration.TotalSeconds > 0 
            ? (Position.TotalSeconds / Duration.TotalSeconds) * 100 
            : 0;
    }
}
