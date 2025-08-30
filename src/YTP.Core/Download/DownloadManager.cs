using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YTP.Core.Models;
using YTP.Core.Services;

namespace YTP.Core.Download
{
    public class DownloadProgress
    {
    public VideoItem? Item { get; init; }
        public double Percentage { get; set; }
        public string? CurrentPhase { get; set; }
        public int TotalItems { get; set; }
        public int CurrentIndex { get; set; }
        public TimeSpan TotalElapsed { get; set; }
        public TimeSpan ItemElapsed { get; set; }
    }

    public class DownloadManager
    {
        private readonly IYoutubeService _yts;
        private readonly FFmpegService _ffmpeg;
        private readonly string _outputDir;
    private readonly System.Threading.ManualResetEventSlim _pauseEvent = new(true);

        public event Action<DownloadProgress>? ProgressChanged;
        public event Action<string>? LogMessage;

        private readonly Func<FFmpegService, MetadataService, YoutubeDownloaderService>? _downloaderFactory;

        public DownloadManager(IYoutubeService yts, FFmpegService ffmpeg, string outputDir, Func<FFmpegService, MetadataService, YoutubeDownloaderService>? downloaderFactory = null)
        {
            _yts = yts;
            _ffmpeg = ffmpeg;
            _outputDir = outputDir;
            _downloaderFactory = downloaderFactory;
        }

    public void Pause() => _pauseEvent.Reset();
    public void Resume() => _pauseEvent.Set();

        public async Task DownloadQueueAsync(string[] urls, CancellationToken ct = default)
        {
            // Run processing on a background thread to avoid capturing UI synchronization context
            await Task.Run(async () => {
                var idx = 0;
                var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
                foreach (var url in urls)
                {
                    ct.ThrowIfCancellationRequested();
                    idx++;
                    LogMessage?.Invoke($"Processing {idx}/{urls.Length}: {url}");

                    var items = await _yts.GetPlaylistOrVideoAsync(url, ct).ConfigureAwait(false);
                    var totalItems = items.Count;
                    var completed = 0;
                    foreach (var item in items)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Wait while paused (runs on background thread)
                        _pauseEvent.Wait(ct);
                        var itemStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var progress = new DownloadProgress { Item = item, Percentage = 0, CurrentPhase = "queued", TotalItems = totalItems, CurrentIndex = completed + 1, TotalElapsed = totalStopwatch.Elapsed, ItemElapsed = TimeSpan.Zero };
                        ProgressChanged?.Invoke(progress);

                        try
                        {
                            // Real download
                            var metadataService = new MetadataService();
                            var downloader = _downloaderFactory != null ? _downloaderFactory(_ffmpeg, metadataService) : new YoutubeDownloaderService(_ffmpeg, metadataService);
                            progress.CurrentPhase = "downloading";
                            progress.TotalElapsed = totalStopwatch.Elapsed;
                            progress.ItemElapsed = itemStopwatch.Elapsed;
                            ProgressChanged?.Invoke(progress);

                            var playlistFolder = item.PlaylistTitle;
                            var template = item.IsPlaylistItem ? "{track} - {artist} - {title}" : "{artist} - {title}";
                            // Map per-item progress (0..1) to overall progress across playlist
                            var itemProgress = new Progress<double>(p => {
                                // Ensure callback runs on a threadpool thread to avoid UI synchronization capture
                                System.Threading.ThreadPool.QueueUserWorkItem(_ => {
                                    try
                                    {
                                        _pauseEvent.Wait(ct);
                                        var overall = (completed + p) / (double)totalItems;
                                        progress.Percentage = overall;
                                        progress.TotalElapsed = totalStopwatch.Elapsed;
                                        progress.ItemElapsed = itemStopwatch.Elapsed;
                                        ProgressChanged?.Invoke(progress);
                                    }
                                    catch (OperationCanceledException) { }
                                });
                            });

                            var mp3Path = await downloader.DownloadAudioAsMp3Async(item, _outputDir, "320k", playlistFolder, template, ct, itemProgress).ConfigureAwait(false);

                            progress.CurrentPhase = "tagging";
                            progress.Percentage = (completed + 0.95) / (double)totalItems;
                            progress.ItemElapsed = itemStopwatch.Elapsed;
                            progress.TotalElapsed = totalStopwatch.Elapsed;
                            ProgressChanged?.Invoke(progress);

                            // Finalize
                            progress.CurrentPhase = "completed";
                            completed++;
                            progress.Percentage = completed / (double)totalItems;
                            progress.ItemElapsed = itemStopwatch.Elapsed;
                            progress.TotalElapsed = totalStopwatch.Elapsed;
                            ProgressChanged?.Invoke(progress);
                            itemStopwatch.Stop();
                            LogMessage?.Invoke($"Completed: {item.Title} -> {mp3Path}");
                        }
                        catch (OperationCanceledException)
                        {
                            LogMessage?.Invoke($"Cancelled: {item.Title}");
                            totalStopwatch.Stop();
                            throw;
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke($"Error processing {item.Title}: {ex.Message}");
                        }
                    }
                    // after finishing this url/playlist update total stopwatch if we continue
                    if (idx == urls.Length)
                    {
                        totalStopwatch.Stop();
                    }
                }
            }, ct).ConfigureAwait(false);
        }

        private string SanitizeFilename(string filename)
        {
            // First clean common channel/artist suffixes
            filename = YTP.Core.Utilities.NameCleaner.CleanName(filename);
            foreach (var c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
            if (filename.Length > 200) filename = filename.Substring(0, 200);
            return filename.Trim();
        }
    }
}
