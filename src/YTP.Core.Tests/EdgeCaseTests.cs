using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using YTP.Core.Download;
using YTP.Core.Services;

namespace YTP.Core.Tests
{
    public class EdgeCaseTests
    {
        [Fact]
        public async Task DownloadManager_CancelDuringPause_CancelsGracefully()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_cancel_pause");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var yts = new PauseResumeTests.SingleVideoYoutubeService();
            var ff = new FFmpegService("ffmpeg");
            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m) => new PauseResumeTests.SlowFakeDownloaderService(TimeSpan.FromMilliseconds(200));

            var dm = new DownloadManager(yts, ff, tempDir, FakeFactory);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var task = dm.DownloadQueueAsync(new[] { "https://youtube.com/watch?v=FAKE" }, cts.Token);

            await Task.Delay(250);
            dm.Pause();

            // cancel while paused
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task DownloadManager_MultiplePauseResume_Cycles_Completes()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_multi_pause");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var yts = new PauseResumeTests.SingleVideoYoutubeService();
            var ff = new FFmpegService("ffmpeg");
            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m) => new PauseResumeTests.SlowFakeDownloaderService(TimeSpan.FromMilliseconds(80));

            var dm = new DownloadManager(yts, ff, tempDir, FakeFactory);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var task = dm.DownloadQueueAsync(new[] { "https://youtube.com/watch?v=FAKE" }, cts.Token);

            // cycle pause/resume a few times
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(150);
                dm.Pause();
                await Task.Delay(120);
                dm.Resume();
            }

            await task; // should complete

            Directory.Delete(tempDir, true);
        }
    }
}
