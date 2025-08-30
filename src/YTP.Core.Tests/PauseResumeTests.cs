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
    public class PauseResumeTests
    {
        [Fact]
        public async Task DownloadManager_PauseAndResume_StopsAndContinuesProgress()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ytp_test_pause_resume");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var yts = new SingleVideoYoutubeService();
            var ffmpeg = new FFmpegService("ffmpeg");

            // Fake downloader that reports progress in 10 steps with delays
            YoutubeDownloaderService FakeFactory(FFmpegService f, MetadataService m)
            {
                return new SlowFakeDownloaderService(TimeSpan.FromMilliseconds(100));
            }

            var dm = new DownloadManager(yts, ffmpeg, tempDir, FakeFactory);

            double lastProgress = 0;
            var progressUpdates = 0;
            var progressLock = new object();

            dm.ProgressChanged += p => {
                lock (progressLock)
                {
                    lastProgress = Math.Max(lastProgress, p.Percentage);
                    progressUpdates++;
                }
            };

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Start download in background
            var downloadTask = dm.DownloadQueueAsync(new[] { "https://youtube.com/watch?v=FAKE" }, cts.Token);

            // Wait a bit to let progress start
            await Task.Delay(250);

            // Pause and capture progress at pause time
            dm.Pause();
            double progressAtPause;
            int updatesAtPause;
            lock (progressLock) { progressAtPause = lastProgress; updatesAtPause = progressUpdates; }

            // Wait while paused - progress should not increase
            await Task.Delay(500);
            lock (progressLock) { Assert.Equal(progressAtPause, lastProgress); Assert.Equal(updatesAtPause, progressUpdates); }

            // Resume and wait for completion
            dm.Resume();
            await downloadTask; // will throw if cancelled

            // Ensure progress increased after resume and final progress reached 1.0 (or close)
            lock (progressLock)
            {
                Assert.True(lastProgress > progressAtPause, "Progress did not increase after resume");
                Assert.True(lastProgress >= 0.99 || progressUpdates > updatesAtPause, "Did not see completion progress or further updates");
            }

            // cleanup
            Directory.Delete(tempDir, true);
        }

        private class SingleVideoYoutubeService : IYoutubeService
        {
            public Task<VideoItem> GetVideoMetadataAsync(string videoId, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public Task<System.Collections.Generic.IReadOnlyList<VideoItem>> GetPlaylistOrVideoAsync(string url, CancellationToken ct = default)
            {
                var v = new VideoItem { Id = "x1", Title = "Slow Song", Channel = "Artist", IsPlaylistItem = false };
                return Task.FromResult((System.Collections.Generic.IReadOnlyList<VideoItem>)new[] { v });
            }
        }

        private class SlowFakeDownloaderService : YoutubeDownloaderService
        {
            private readonly TimeSpan _stepDelay;
            public SlowFakeDownloaderService(TimeSpan stepDelay) : base(null!, null!) { _stepDelay = stepDelay; }

            public override async Task<string> DownloadAudioAsMp3Async(VideoItem item, string outputDirectory, string mp3Quality = "320k", string? playlistFolderName = null, string? filenameTemplate = null, CancellationToken ct = default, IProgress<double>? progress = null)
            {
                var targetDir = outputDirectory;
                if (!string.IsNullOrWhiteSpace(playlistFolderName)) targetDir = Path.Combine(outputDirectory, playlistFolderName);
                Directory.CreateDirectory(targetDir);
                var path = Path.Combine(targetDir, "slow.mp3");

                // Simulate 10 progress steps
                for (int i = 0; i <= 10; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(i / 10.0);
                    await Task.Delay(_stepDelay, ct);
                }

                // write file
                File.WriteAllText(path, "dummy");
                return path;
            }
        }
    }
}
