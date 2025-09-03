using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using YTP.Core.Download;
using YTP.Core.Models;
using YTP.Core.Services;

namespace YTP.Core.Tests
{
    public class PlaylistDownloadTests
    {
        [Fact]
        public async Task DownloadManager_CreatesPlaylistFolderAndFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_output");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            // Mock youtube service returns two items belonging to playlist
            var yts = new FakeYoutubeService();
            var ffmpeg = new FFmpegService("ffmpeg");

            // Fake downloader that writes a dummy mp3 file
            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m)
            {
                return new FakeDownloaderService();
            }

            var dm = new DownloadManager(yts, ffmpeg, tempDir, FakeFactory);
            dm.LogMessage += s => { /* ignore */ };
            dm.ProgressChanged += p => { /* ignore */ };

            await dm.DownloadQueueAsync(new[] { "https://youtube.com/playlist?list=FAKE" }, CancellationToken.None);

            // Assert playlist folder exists and files exist
            var playlistFolder = Path.Combine(tempDir, "My Playlist");
            Assert.True(Directory.Exists(playlistFolder));
            var files = Directory.GetFiles(playlistFolder, "*.mp3");
            Assert.Equal(2, files.Length);

            // cleanup
            Directory.Delete(tempDir, true);
        }

        private class FakeYoutubeService : IYoutubeService
        {
            public Task<VideoItem> GetVideoMetadataAsync(string videoId, CancellationToken ct = default)
            {
                throw new System.NotImplementedException();
            }

            public Task<System.Collections.Generic.IReadOnlyList<VideoItem>> GetPlaylistOrVideoAsync(string url, CancellationToken ct = default)
            {
                var a = new VideoItem { Id = "a1", Title = "Song A", Channel = "Artist A", IsPlaylistItem = true, PlaylistIndex = 1, PlaylistTotal = 2, PlaylistTitle = "My Playlist" };
                var b = new VideoItem { Id = "b2", Title = "Song B", Channel = "Artist B", IsPlaylistItem = true, PlaylistIndex = 2, PlaylistTotal = 2, PlaylistTitle = "My Playlist" };
                return Task.FromResult((System.Collections.Generic.IReadOnlyList<VideoItem>)new[] { a, b });
            }
        }

        private class FakeDownloaderService : YoutubeDownloaderService
        {
            public FakeDownloaderService() : base(null!, null!) { }

            public override Task<string> DownloadAudioAsMp3Async(VideoItem item, string outputDirectory, string mp3Quality = "320k", string? playlistFolderName = null, string? filenameTemplate = null, CancellationToken ct = default, IProgress<double>? progress = null)
            {
                var targetDir = outputDirectory;
                if (!string.IsNullOrWhiteSpace(playlistFolderName)) targetDir = Path.Combine(outputDirectory, playlistFolderName);
                Directory.CreateDirectory(targetDir);
                var fileName = (item.PlaylistIndex.HasValue ? item.PlaylistIndex.Value.ToString("D2") + " - " : "") + item.Channel + " - " + item.Title + ".mp3";
                var path = Path.Combine(targetDir, fileName);
                File.WriteAllText(path, "dummy mp3");
                return Task.FromResult(path);
            }
        }
    }
}
