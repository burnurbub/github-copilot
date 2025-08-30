using System;

namespace YTP.Core.Models
{
    public class VideoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime? UploadDate { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsPlaylistItem { get; set; } = false;
        public int? PlaylistIndex { get; set; }
        public int? PlaylistTotal { get; set; }
    public string? PlaylistTitle { get; set; }
    // Optional music-specific metadata
    public string? Album { get; set; }
    public int? TrackNumber { get; set; }
    public int? Year { get; set; }
    }
}
