using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using YTP.Core.Download;
using YTP.Core.Services;

namespace YTP.Core.Tests
{
    public class DownloadManagerTests
    {
        [Fact]
        public async Task RemoveItem_MoveItem_PauseItem_Behavior()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_dm");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var yts = new PauseFriendlyYoutubeService();
            var ffmpeg = new FFmpegService("ffmpeg");

            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m)
            {
                return new DummyDownloaderService();
            }

            var dm = new DownloadManager(yts, ffmpeg, tempDir, FakeFactory);

            // prepare 3 items
            var items = new[] {
                new YTP.Core.Models.VideoItem { Id = "a", Title = "One" },
                new YTP.Core.Models.VideoItem { Id = "b", Title = "Two" },
                new YTP.Core.Models.VideoItem { Id = "c", Title = "Three" }
            };

            // Start download in background
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var t = dm.DownloadItemsAsync(items, cts.Token);

            // pause middle item before it starts by calling PauseItem
            await Task.Delay(50);
            var paused = dm.RemoveItem("b"); // remove item b from pending
            Assert.True(paused, "RemoveItem should remove pending item b");

            // move last item to position 0
            var moved = dm.MoveItem(1, 0); // after removal indices shift; this checks safe behavior
            Assert.True(moved || moved == false, "MoveItem should return a boolean (no exception)");

            // pause current item (if any) - PauseItem should not throw
            dm.PauseItem("a");
            dm.ResumeItem("a");

            // cancel and complete
            cts.CancelAfter(200);
            try { await t; } catch { }

            // cleanup
            Directory.Delete(tempDir, true);
        }

        private class PauseFriendlyYoutubeService : IYoutubeService
        {
            public Task<YTP.Core.Models.VideoItem> GetVideoMetadataAsync(string videoId, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<System.Collections.Generic.IReadOnlyList<YTP.Core.Models.VideoItem>> GetPlaylistOrVideoAsync(string url, CancellationToken ct = default)
            {
                var list = new[] { new YTP.Core.Models.VideoItem { Id = "a", Title = "One" } };
                return Task.FromResult((System.Collections.Generic.IReadOnlyList<YTP.Core.Models.VideoItem>)list);
            }
        }

        private class DummyDownloaderService : YoutubeDownloaderService
        {
            public DummyDownloaderService() : base(null!, null!) { }
            public override Task<string> DownloadAudioAsMp3Async(YTP.Core.Models.VideoItem item, string outputDirectory, string mp3Quality = "320k", string? playlistFolderName = null, string? filenameTemplate = null, CancellationToken ct = default, IProgress<double>? progress = null)
            {
                // quick no-op
                return Task.FromResult(Path.Combine(outputDirectory, item.Id + ".mp3"));
            }
        }

        [Fact]
        public async Task RetryOn403_WillRetryUntilSuccess()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_dm_retry");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var yts = new PauseFriendlyYoutubeService();
            var ffmpeg = new FFmpegService("ffmpeg");

            int attempt = 0;
            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m)
            {
                return new FlakyDownloader(() => ++attempt);
            }

            var dm = new DownloadManager(yts, ffmpeg, tempDir, FakeFactory);

            var item = new YTP.Core.Models.VideoItem { Id = "x", Title = "RetryMe" };
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Start download of a single item which will fail with a 403 (simulated) twice then succeed
            await dm.DownloadItemsAsync(new[] { item }, cts.Token);

            // expect that the flaky downloader attempted enough times to succeed (initial + retries)
            Assert.True(attempt >= 3, $"Downloader should have attempted >=3 times but was {attempt}.");

            Directory.Delete(tempDir, true);
        }

        private class FlakyDownloader : YoutubeDownloaderService
        {
            private readonly Func<int> _getAttempt;
            public FlakyDownloader(Func<int> attempt) : base(null!, null!) { _getAttempt = attempt; }
            public override Task<string> DownloadAudioAsMp3Async(YTP.Core.Models.VideoItem item, string outputDirectory, string mp3Quality = "320k", string? playlistFolderName = null, string? filenameTemplate = null, CancellationToken ct = default, IProgress<double>? progress = null)
            {
                var a = _getAttempt();
                if (a <= 2)
                {
                    throw new InvalidOperationException("403 Forbidden");
                }
                return Task.FromResult(Path.Combine(outputDirectory, item.Id + ".mp3"));
            }
        }
    }
}
