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
    // per-item control (true == paused)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _pausedItems = new();
    // cancellation for the currently-processing item
    private CancellationTokenSource? _currentItemCts;
    private readonly object _pendingLock = new();
    private readonly System.Collections.Generic.List<YTP.Core.Models.VideoItem> _pending = new();
    private int _pendingIndex = 0;
    // signal that pending items are available (used when queue can be appended at runtime)
    private readonly System.Threading.ManualResetEventSlim _hasPending = new(false);
    // items that failed due to 403/forbidden and should be retried later
    private readonly System.Collections.Generic.List<YTP.Core.Models.VideoItem> _forbidden = new();
    
    /// <summary>
    /// Append items to the pending queue while the manager is running.
    /// Thread-safe and will be visible to a running DownloadItemsAsync consumer.
    /// </summary>
    public void AddItems(System.Collections.Generic.IEnumerable<YTP.Core.Models.VideoItem> items)
    {
        if (items == null) return;
        lock (_pendingLock)
        {
            _pending.AddRange(items);
            _hasPending.Set();
        }
    }

    /// <summary>
    /// Append a single item to the pending queue.
    /// </summary>
    public void AddItem(YTP.Core.Models.VideoItem item)
    {
        if (item == null) return;
        lock (_pendingLock)
        {
            _pending.Add(item);
            _hasPending.Set();
        }
    }

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

    // Pause and resume a specific item by id. When paused, the manager will wait before starting that item.
    public void PauseItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
    _pausedItems[itemId] = true;
    // Do NOT engage global pause here. Pausing an item should be per-item only.
    // If it's the currently processing item, the DownloadItemsAsync loop and progress callbacks
    // will observe _pausedItems and wait cooperatingly.
    }

    public void ResumeItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        _pausedItems.TryRemove(itemId, out _);
        // if no paused items remain, resume global flow
        if (_pausedItems.IsEmpty)
            _pauseEvent.Set();
    }

    // Skip the currently-processing item by cancelling its per-item token; manager will continue to next
    public void SkipCurrent()
    {
        try
        {
            _currentItemCts?.Cancel();
        }
        catch { }
    }

    // Remove an item from the pending queue. Returns true if removed.
    public bool RemoveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        lock (_pendingLock)
        {
            var idx = _pending.FindIndex(i => i.Id == itemId);
            if (idx >= 0)
            {
                _pending.RemoveAt(idx);
                if (idx < _pendingIndex) _pendingIndex--; // adjust pointer
                return true;
            }
        }
        return false;
    }

    // Move an item inside the pending queue (used for drag/reorder)
                int processedCount = 0;
                int retriedCount = 0;
                int succeededAfterRetry = 0;
    public bool MoveItem(int oldIndex, int newIndex)
    {
        lock (_pendingLock)
        {
            if (oldIndex < 0 || oldIndex >= _pending.Count || newIndex < 0 || newIndex > _pending.Count - 1) return false;
            var item = _pending[oldIndex];
            _pending.RemoveAt(oldIndex);
            _pending.Insert(newIndex, item);
            return true;
        }
    }

    // Move an item by id to a new index in the pending queue. Returns true if moved.
    public bool MoveItemById(string itemId, int newIndex)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        lock (_pendingLock)
        {
            var idx = _pending.FindIndex(i => i.Id == itemId);
            if (idx < 0) return false;
            if (newIndex < 0) newIndex = 0;
            if (newIndex > _pending.Count - 1) newIndex = _pending.Count - 1;
            var item = _pending[idx];
            _pending.RemoveAt(idx);
            if (newIndex > _pending.Count) newIndex = _pending.Count;
            _pending.Insert(newIndex, item);
            return true;
        }
    }

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

        // New API: download a prepared list of VideoItem instances (flattened playlist items).
        public async Task DownloadItemsAsync(System.Collections.Generic.IEnumerable<YTP.Core.Models.VideoItem> items, CancellationToken ct = default)
        {
            // copy into pending list so runtime removals/moves are possible
            lock (_pendingLock)
            {
                    processedCount++;
                _pending.Clear();
                _pending.AddRange(items ?? System.Array.Empty<YTP.Core.Models.VideoItem>());
                _pendingIndex = 0;
            }
            // signal that pending items are available
            _hasPending.Set();

            await Task.Run(async () => {
                var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    retriedCount++;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    // If no pending items are available, wait until AddItem/AddItems signals availability
                    lock (_pendingLock)
                    {
                        if (_pendingIndex >= _pending.Count)
                        {
                            // reset signal and wait outside lock
                            _hasPending.Reset();
                        }
                    }

                    // Wait for new items or cancellation
                    if (_pendingIndex >= _pending.Count)
                    {
                        // Wait with cancellation support
                        try { _hasPending.Wait(ct); }
                        catch (OperationCanceledException) { throw; }
                    }

                    YTP.Core.Models.VideoItem? item = null;
                    int currentIndex = 0;
                    int totalItems = 0;
                    lock (_pendingLock)
                    {
                        totalItems = _pending.Count;
                        if (_pendingIndex >= totalItems) continue; // re-check after waiting
                        item = _pending[_pendingIndex];
                        currentIndex = _pendingIndex + 1;
                        // advance pointer optimistically; removals will adjust in RemoveItem
                        _pendingIndex++;
                    }

                    if (item == null) break;

                    LogMessage?.Invoke($"Processing {currentIndex}/{totalItems}: {item.Title}");

                    try
                    {
                        // wait for global pause
                        _pauseEvent.Wait(ct);

                        // wait while this specific item is paused (per-item pause)
                        while (_pausedItems.ContainsKey(item.Id))
                        {
                            ct.ThrowIfCancellationRequested();
                            System.Threading.Thread.Sleep(150);
                        }

                        var itemStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var progress = new DownloadProgress { Item = item, Percentage = 0, CurrentPhase = "queued", TotalItems = totalItems, CurrentIndex = currentIndex, TotalElapsed = totalStopwatch.Elapsed, ItemElapsed = TimeSpan.Zero };
                        ProgressChanged?.Invoke(progress);

                        // Real download
                        var metadataService = new MetadataService();
                        var downloader = _downloaderFactory != null ? _downloaderFactory(_ffmpeg, metadataService) : new YoutubeDownloaderService(_ffmpeg, metadataService);
                        progress.CurrentPhase = "downloading";
                        progress.TotalElapsed = totalStopwatch.Elapsed;
                        progress.ItemElapsed = itemStopwatch.Elapsed;
                        ProgressChanged?.Invoke(progress);

                        var playlistFolder = item.PlaylistTitle;
                        var template = item.IsPlaylistItem ? "{track} - {artist} - {title}" : "{artist} - {title}";

                        // create a CTS for the current item so SkipCurrent can cancel it
                        _currentItemCts?.Dispose();
                        _currentItemCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var itemCt = _currentItemCts.Token;

                        var itemProgress = new Progress<double>(p => {
                            System.Threading.ThreadPool.QueueUserWorkItem(_ => {
                                try
                                {
                                    // respect both global pause and per-item pause while reporting
                                    _pauseEvent.Wait(itemCt);
                                    while (_pausedItems.ContainsKey(item.Id))
                                    {
                                        itemCt.ThrowIfCancellationRequested();
                                        System.Threading.Thread.Sleep(150);
                                    }
                                    var overall = (currentIndex - 1 + p) / (double)totalItems;
                                    progress.Percentage = overall;
                                    progress.TotalElapsed = totalStopwatch.Elapsed;
                                    progress.ItemElapsed = itemStopwatch.Elapsed;
                                    ProgressChanged?.Invoke(progress);
                                }
                                catch (OperationCanceledException) { }
                            });
                        });

                            var mp3Path = await downloader.DownloadAudioAsMp3Async(item, _outputDir, "320k", playlistFolder, template, itemCt, itemProgress).ConfigureAwait(false);

                        progress.CurrentPhase = "tagging";
                        progress.Percentage = (currentIndex - 1 + 0.95) / (double)totalItems;
                        progress.ItemElapsed = itemStopwatch.Elapsed;
                        progress.TotalElapsed = totalStopwatch.Elapsed;
                        ProgressChanged?.Invoke(progress);

                        // Finalize
                        progress.CurrentPhase = "completed";
                        progress.Percentage = currentIndex / (double)totalItems;
                        progress.ItemElapsed = itemStopwatch.Elapsed;
                        progress.TotalElapsed = totalStopwatch.Elapsed;
                        ProgressChanged?.Invoke(progress);
                        itemStopwatch.Stop();
                        LogMessage?.Invoke($"Completed: {item.Title} -> {mp3Path}");
                        // clear current item cancellation token source
                        try { _currentItemCts?.Dispose(); _currentItemCts = null; } catch { }
                    }
                        catch (OperationCanceledException)
                    {
                        LogMessage?.Invoke($"Cancelled: {item.Title}");
                        totalStopwatch.Stop();
                        throw;
                    }
                        catch (Exception ex)
                        {
                            // detect Forbidden/403-like errors from underlying libraries
                            var msg = ex.Message ?? string.Empty;
                            if (msg.Contains("403") || msg.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                LogMessage?.Invoke($"403 Forbidden for {item.Title}; will retry later.");
                                lock (_pendingLock) { _forbidden.Add(item); }
                            }
                            else
                            {
                                LogMessage?.Invoke($"Error processing {item.Title}: {ex.Message}");
                            }
                        }
                }

                totalStopwatch.Stop();
                // After finishing the main pass, retry any 403/forbidden items
                if (_forbidden.Count > 0)
                {
                    var retryList = new System.Collections.Generic.List<YTP.Core.Models.VideoItem>(_forbidden);
                    _forbidden.Clear();
                    LogMessage?.Invoke($"Retrying {retryList.Count} previously-forbidden items...");
                    foreach (var item in retryList)
                    {
                        try
                        {
                            // small retry attempts (3)
                            var attempts = 0;
                            var succeeded = false;
                            while (attempts < 3 && !succeeded)
                            {
                                attempts++;
                                try
                                {
                                    // perform a single-item download using same logic as above but without progress reporting
                                    var metadataService = new MetadataService();
                                    var downloader = _downloaderFactory != null ? _downloaderFactory(_ffmpeg, metadataService) : new YoutubeDownloaderService(_ffmpeg, metadataService);
                                    var playlistFolder = item.PlaylistTitle;
                                    var template = item.IsPlaylistItem ? "{track} - {artist} - {title}" : "{artist} - {title}";
                                    var cts = CancellationToken.None;
                                    var tmpProgress = new Progress<double>();
                                    var mp3Path = await downloader.DownloadAudioAsMp3Async(item, _outputDir, "320k", playlistFolder, template, cts, tmpProgress).ConfigureAwait(false);
                                    // verify file exists and is non-empty
                                    if (!string.IsNullOrEmpty(mp3Path) && System.IO.File.Exists(mp3Path) && new System.IO.FileInfo(mp3Path).Length > 0)
                                    {
                                        LogMessage?.Invoke($"Retry succeeded: {item.Title} -> {mp3Path}");
                                        succeeded = true;
                                        break;
                                    }
                                    else
                                    {
                                        LogMessage?.Invoke($"Retry attempt {attempts} for {item.Title} succeeded but file verification failed.");
                                    }
                                }
                                catch (Exception rex)
                                {
                                    LogMessage?.Invoke($"Retry attempt {attempts} for {item.Title} failed: {rex.Message}");
                                    // exponential backoff
                                    await Task.Delay(1000 * attempts).ConfigureAwait(false);
                                }
                            }
                            if (!succeeded) LogMessage?.Invoke($"Failed after retries: {item.Title}");
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke($"Unexpected error retrying {item.Title}: {ex.Message}");
                        }
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
