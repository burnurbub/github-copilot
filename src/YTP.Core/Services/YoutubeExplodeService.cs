using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YTP.Core.Models;

namespace YTP.Core.Services
{
    public class YoutubeExplodeService : IYoutubeService
    {
        private readonly YoutubeClient _client;

        public YoutubeExplodeService()
        {
            _client = new YoutubeClient();
        }

        private static string? GetBestThumbnailUrl(IReadOnlyList<YoutubeExplode.Common.Thumbnail> thumbs)
        {
            if (thumbs == null || thumbs.Count == 0) return null;
            // Choose thumbnail with largest width*height
            var best = thumbs.OrderByDescending(t => (t.Resolution.Width * t.Resolution.Height)).FirstOrDefault();
            return best?.Url;
        }

        public async Task<IReadOnlyList<VideoItem>> GetPlaylistOrVideoAsync(string url, CancellationToken ct = default)
        {
            // Try playlist first
            try
            {
                var playlistId = PlaylistId.TryParse(url);
                if (playlistId != null)
                {
                    var playlist = await _client.Playlists.GetAsync(playlistId.Value, ct);
                    var videos = await _client.Playlists.GetVideosAsync(playlistId.Value, ct);
                    var list = videos.Select((v, idx) => new VideoItem
                    {
                        Id = v.Id.Value,
                        Title = v.Title,
                        // Playlist entries from the API may not include full metadata like description or thumbnails.
                        Channel = v.Author?.ChannelTitle ?? string.Empty,
                        Description = null,
                        ThumbnailUrl = GetBestThumbnailUrl(v.Thumbnails),
                        UploadDate = null,
                        Duration = v.Duration,
                        IsPlaylistItem = true,
                        PlaylistIndex = idx + 1,
                        PlaylistTitle = playlist.Title
                    }).ToList();
                    // Populate PlaylistTotal afterwards
                    for (int i = 0; i < list.Count; i++) list[i].PlaylistTotal = list.Count;
                    return list;
                }
            }
            catch
            {
                // Not a playlist or error; fall through to video
            }

            // Try single video
            try
            {
                var videoId = VideoId.Parse(url);
                var v = await _client.Videos.GetAsync(videoId, ct);
                var item = new VideoItem
                {
                    Id = v.Id.Value,
                    Title = v.Title,
                    Channel = v.Author?.ChannelTitle ?? string.Empty,
                    Description = v.Description,
                    ThumbnailUrl = GetBestThumbnailUrl(v.Thumbnails),
                    UploadDate = v.UploadDate.UtcDateTime,
                    Duration = v.Duration,
                    IsPlaylistItem = false
                };
                return new[] { item };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not parse URL or fetch metadata.", ex);
            }
        }

        public async Task<VideoItem> GetVideoMetadataAsync(string videoId, CancellationToken ct = default)
        {
            var v = await _client.Videos.GetAsync(videoId, ct);
            return new VideoItem
            {
                Id = v.Id.Value,
                Title = v.Title,
                    Channel = v.Author?.ChannelTitle ?? string.Empty,
                Description = v.Description,
                    ThumbnailUrl = GetBestThumbnailUrl(v.Thumbnails),
                Duration = v.Duration
            };
        }
    }
}
