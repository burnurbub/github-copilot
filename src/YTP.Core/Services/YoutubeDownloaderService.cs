using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using YTP.Core.Models;

namespace YTP.Core.Services
{
    public class YoutubeDownloaderService
    {
        private readonly YoutubeClient _client;
        private readonly FFmpegService _ffmpeg;
        private readonly MetadataService _meta;

        public YoutubeDownloaderService(FFmpegService ffmpeg, MetadataService meta)
        {
            _client = new YoutubeClient();
            _ffmpeg = ffmpeg;
            _meta = meta;
        }

        /// <summary>
        /// Downloads the best audio, optionally converts to mp3 and tags the file.
        /// Returns the path to the final mp3 file (or original file if not converted).
        /// </summary>
        /// <param name="playlistFolderName">Optional folder name (for playlists) - if provided downloads go into outputDirectory/playlistFolderName</param>
        /// <param name="filenameTemplate">Optional template, use {track} {artist} - {title}</param>
    public virtual async Task<string> DownloadAudioAsMp3Async(VideoItem item, string outputDirectory, string mp3Quality = "320k", string? playlistFolderName = null, string? filenameTemplate = null, CancellationToken ct = default, IProgress<double>? progress = null)
        {
            var targetDir = outputDirectory;
            if (!string.IsNullOrWhiteSpace(playlistFolderName))
            {
                targetDir = Path.Combine(outputDirectory, SanitizeFilename(playlistFolderName));
            }
            Directory.CreateDirectory(targetDir);

            // Build filename
            var artist = string.IsNullOrWhiteSpace(item.Channel) ? "Unknown Artist" : YTP.Core.Utilities.NameCleaner.CleanName(item.Channel);
            var title = YTP.Core.Utilities.NameCleaner.CleanName(item.Title);
            var track = item.PlaylistIndex.HasValue ? item.PlaylistIndex.Value.ToString("D2") : null;

            filenameTemplate ??= "{track} - {artist} - {title}";
            var fileBase = filenameTemplate.Replace("{artist}", SanitizeFilename(artist)).Replace("{title}", SanitizeFilename(title));
            if (fileBase.Contains("{track}") && track != null) fileBase = fileBase.Replace("{track}", track);
            // Remove any leftover placeholders
            fileBase = fileBase.Replace("{track}", "").Trim();

            var tempFile = Path.Combine(targetDir, fileBase + ".audio");
            var finalMp3 = Path.Combine(targetDir, fileBase + ".mp3");

            // Get stream manifest
            var vidId = item.Id;
            var streamManifest = await _client.Videos.Streams.GetManifestAsync(vidId, ct);
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();
            if (audioStreamInfo == null)
                throw new InvalidOperationException("No audio stream available.");

            // Download audio to tempFile with extension matching container
            var containerExt = audioStreamInfo.Container.Name; // e.g., mp4, webm
            var downloadPath = tempFile + "." + containerExt;
            await _client.Videos.Streams.DownloadAsync(audioStreamInfo, downloadPath, cancellationToken: ct);
            progress?.Report(0.5);

            // Convert to mp3 using ffmpeg
            await _ffmpeg.ConvertToMp3Async(downloadPath, finalMp3, mp3Quality, ct);
            progress?.Report(0.8);

            // Clean up downloaded container
            try { File.Delete(downloadPath); } catch { }

            // Fetch album art and lyrics
            byte[]? art = null;
            if (!string.IsNullOrEmpty(item.ThumbnailUrl)) art = await _meta.FetchAndPrepareAlbumArtAsync(item.ThumbnailUrl, ct);
            var lyrics = await _meta.ScrapeLyricsAsync(item.Title, item.Channel, item.Description, ct);

            // Try to fetch synced lines (may be null)
            var synced = await _meta.FetchSyncedLyricsAsync(item.Id, ct);

            // Tag (pass synced lines to create .lrc sidecar)
            await _meta.TagMp3(finalMp3, item, art, lyrics, synced);
            progress?.Report(1.0);

            return finalMp3;
        }

        private string SanitizeFilename(string filename)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
            if (filename.Length > 200) filename = filename.Substring(0, 200);
            return filename.Trim();
        }
    }
}
