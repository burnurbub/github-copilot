using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YTP.Core.Models;

namespace YTP.Core.Services
{
    public interface IYoutubeService
    {
        /// <summary>
        /// If url is a playlist, returns all video ids/titles in the playlist. For single videos returns a single item.
        /// </summary>
        Task<IReadOnlyList<VideoItem>> GetPlaylistOrVideoAsync(string url, CancellationToken ct = default);

        /// <summary>
        /// Fetches full metadata for a single video id.
        /// </summary>
        Task<VideoItem> GetVideoMetadataAsync(string videoId, CancellationToken ct = default);
    }
}
