using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using YTP.Core.Services;
using YTP.Core.Settings;
using YTP.Core.Download;
using YTP.Core.Models;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Timers;

namespace YTP.MacUI
{
    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settings = new();
        private DownloadManager? _dm;
        private FFmpegService? _ffmpeg;
        private IYoutubeService? _yts;
        private CancellationTokenSource? _cts;

    private System.Timers.Timer _totalTimer;
    private System.Timers.Timer _itemTimer;

    // controls
    private Avalonia.Controls.TextBox? _outputDirText;
    private Avalonia.Controls.ItemsControl? _queueSummaryCtrl;
    private Avalonia.Controls.TextBox? _urlTextBoxCtrl;
    private Avalonia.Controls.Button? _startButtonCtrl;
    private Avalonia.Controls.Button? _pauseButtonCtrl;
    private Avalonia.Controls.Button? _abortButtonCtrl;
    private Avalonia.Controls.Button? _skipButtonCtrl;
    private Avalonia.Controls.ProgressBar? _itemProgressCtrl;
    private Avalonia.Controls.TextBox? _logTextCtrl;
    private Avalonia.Controls.TextBlock? _totalElapsedTextCtrl;
    private Avalonia.Controls.TextBlock? _itemElapsedTextCtrl;
    private Avalonia.Controls.TextBlock? _currentSongTextCtrl;
    private Avalonia.Controls.TextBlock? _playlistInfoTextCtrl;
        private DateTime? _totalStart;
        private DateTime? _itemStart;

        // Observable queue view models
        private ObservableCollection<VideoItem> _queue = new();

        public MainWindow()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            DataContext = this;

            // resolve named controls (use FindControl since XAML codegen for fields may not be present)
            _outputDirText = this.FindControl<Avalonia.Controls.TextBox>("OutputDirText");
            _queueSummaryCtrl = this.FindControl<Avalonia.Controls.ItemsControl>("QueueSummary");
            _urlTextBoxCtrl = this.FindControl<Avalonia.Controls.TextBox>("UrlTextBox");
            _startButtonCtrl = this.FindControl<Avalonia.Controls.Button>("StartButton");
            _pauseButtonCtrl = this.FindControl<Avalonia.Controls.Button>("PauseButton");
            _abortButtonCtrl = this.FindControl<Avalonia.Controls.Button>("AbortButton");
            _skipButtonCtrl = this.FindControl<Avalonia.Controls.Button>("SkipButton");
            _itemProgressCtrl = this.FindControl<Avalonia.Controls.ProgressBar>("ItemProgress");
            _logTextCtrl = this.FindControl<Avalonia.Controls.TextBox>("LogText");
            _totalElapsedTextCtrl = this.FindControl<Avalonia.Controls.TextBlock>("TotalElapsedText");
            _itemElapsedTextCtrl = this.FindControl<Avalonia.Controls.TextBlock>("ItemElapsedText");
            _currentSongTextCtrl = this.FindControl<Avalonia.Controls.TextBlock>("CurrentSongText");
            _playlistInfoTextCtrl = this.FindControl<Avalonia.Controls.TextBlock>("PlaylistInfoText");

            if (_outputDirText != null) _outputDirText.Text = _settings.Settings.OutputDirectory;

            _totalTimer = new System.Timers.Timer(1000);
            _totalTimer.Elapsed += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_totalStart.HasValue && _totalElapsedTextCtrl != null) _totalElapsedTextCtrl.Text = (DateTime.Now - _totalStart.Value).ToString(@"hh\:mm\:ss");
            });

            _itemTimer = new System.Timers.Timer(1000);
            _itemTimer.Elapsed += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_itemStart.HasValue && _itemElapsedTextCtrl != null) _itemElapsedTextCtrl.Text = (DateTime.Now - _itemStart.Value).ToString(@"hh\:mm\:ss");
            });

            // bind summary control
            if (_queueSummaryCtrl != null) _queueSummaryCtrl.ItemsSource = _queue;

            // Try to enable window transparency on macOS for blur-like effect: set Background to Transparent and allow transparency on window
            try
            {
                this.Background = Avalonia.Media.Brushes.Transparent;
            }
            catch { }
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow();
            sw.Show();
        }

        private void OpenQueueButton_Click(object? sender, RoutedEventArgs e)
        {
            var qw = new QueueWindow(_queue);
            qw.OnPauseToggle = (id, pause) => {
                if (_dm == null) return;
                if (pause) _dm.PauseItem(id); else _dm.ResumeItem(id);
            };
            qw.OnRemove = (id) => {
                if (_dm != null)
                {
                    var removed = _dm.RemoveItem(id);
                    if (removed)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            var toRem = _queue.FirstOrDefault(x => x.Id == id);
                            if (toRem != null) _queue.Remove(toRem);
                        });
                    }
                }
            };
            qw.Show();
        }

        private void Log(string msg)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_logTextCtrl != null) _logTextCtrl.Text += msg + "\n";
            });
        }

        private async void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            var urlsRaw = _urlTextBoxCtrl?.Text ?? string.Empty;
            var urls = urlsRaw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToArray();
            if (urls.Length == 0)
            {
                Log("No URLs provided.");
                return;
            }

            if (_startButtonCtrl != null) _startButtonCtrl.IsEnabled = false;
            if (_pauseButtonCtrl != null) _pauseButtonCtrl.IsEnabled = true;
            if (_abortButtonCtrl != null) _abortButtonCtrl.IsEnabled = true;
            _cts = new CancellationTokenSource();

            _yts = new YoutubeExplodeService();
            _ffmpeg = new FFmpegService(_settings.Settings.FfmpegPath);
            _dm = new DownloadManager(_yts, _ffmpeg, _settings.Settings.OutputDirectory);
            _dm.LogMessage += s => Log(s);
            _dm.ProgressChanged += p => {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    if (!_totalStart.HasValue)
                    {
                        _totalStart = DateTime.Now;
                        _totalTimer.Start();
                    }

                    if (_playlistInfoTextCtrl != null) _playlistInfoTextCtrl.Text = p.TotalItems > 0 ? $"{p.CurrentIndex}/{p.TotalItems}" : string.Empty;
                    if (_currentSongTextCtrl != null) _currentSongTextCtrl.Text = p.Item?.Title ?? string.Empty;
                    if (_itemProgressCtrl != null) _itemProgressCtrl.Value = Math.Max(0, Math.Min(1, p.Percentage));

                    if (!_itemStart.HasValue || p.CurrentIndex != 0)
                    {
                        _itemStart = DateTime.Now;
                        _itemTimer.Start();
                    }

                    if (_itemElapsedTextCtrl != null) _itemElapsedTextCtrl.Text = p.ItemElapsed.ToString(@"hh\:mm\:ss");
                    if (_totalElapsedTextCtrl != null) _totalElapsedTextCtrl.Text = p.TotalElapsed.ToString(@"hh\:mm\:ss");

                    if (p.Percentage >= 1.0 && p.TotalItems > 0 && p.CurrentIndex == p.TotalItems)
                    {
                        _itemTimer.Stop();
                        _totalTimer.Stop();
                    }
                });
            };

            // fetch items metadata and start downloads
            var flatItems = new System.Collections.Generic.List<YTP.Core.Models.VideoItem>();
            foreach (var url in urls)
            {
                try
                {
                    var list = await _yts.GetPlaylistOrVideoAsync(url, _cts.Token);
                    foreach (var vi in list)
                    {
                        _queue.Add(vi);
                        flatItems.Add(vi);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to retrieve info for {url}: {ex.Message}");
                }
            }

            try
            {
                await _dm.DownloadItemsAsync(flatItems, _cts.Token);
                Log("Downloads complete.");
            }
            catch (OperationCanceledException)
            {
                Log("Download cancelled.");
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
            finally
            {
                if (_startButtonCtrl != null) _startButtonCtrl.IsEnabled = true;
                if (_pauseButtonCtrl != null) { _pauseButtonCtrl.IsEnabled = false; _pauseButtonCtrl.Content = "Pause"; }
                if (_abortButtonCtrl != null) _abortButtonCtrl.IsEnabled = false;
                if (_skipButtonCtrl != null) _skipButtonCtrl.IsEnabled = false;
                if (_itemProgressCtrl != null) _itemProgressCtrl.Value = 0;
                _itemTimer.Stop();
                _totalTimer.Stop();
                _itemStart = null;
                _totalStart = null;
                if (_urlTextBoxCtrl != null) _urlTextBoxCtrl.Text = string.Empty;
            }
        }

        private void AbortButton_Click(object? sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (_abortButtonCtrl != null) _abortButtonCtrl.IsEnabled = false;
            if (_pauseButtonCtrl != null) { _pauseButtonCtrl.IsEnabled = false; _pauseButtonCtrl.Content = "Pause"; }
        }

        private void PauseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dm != null)
            {
                // toggle global pause
                if (_pauseButtonCtrl != null && _pauseButtonCtrl.Content?.ToString() == "Pause")
                {
                    _dm.Pause();
                    _pauseButtonCtrl.Content = "Resume";
                    Log("Paused");
                }
                else
                {
                    _dm.Resume();
                    if (_pauseButtonCtrl != null) _pauseButtonCtrl.Content = "Pause";
                    Log("Resumed");
                }
            }
        }

        private void ChooseOutputButton_Click(object? sender, RoutedEventArgs e)
        {
            // Avalonia does not include a built-in folder picker cross-platform in older versions; fallback to asking user to type or use default
            // For now, just open the directory in Finder
            var path = _settings.Settings.OutputDirectory ?? Environment.CurrentDirectory;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }

        private void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            var path = _settings.Settings.OutputDirectory ?? Environment.CurrentDirectory;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }

        private void PasteClipboardButton_Click(object? sender, RoutedEventArgs e)
        {
            // Clipboard paste not implemented on mac build here; user can paste manually.
        }

        private void ClearUrlsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_urlTextBoxCtrl != null) _urlTextBoxCtrl.Text = string.Empty;
        }

        private void EnqueueButton_Click(object? sender, RoutedEventArgs e)
        {
            // simply parse lines and add to queue without starting
            var urlsRaw = _urlTextBoxCtrl?.Text ?? string.Empty;
            var urls = urlsRaw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToArray();
            if (urls.Length == 0) return;
            if (_yts == null) _yts = new YoutubeExplodeService();

            _ = Task.Run(async () => {
                foreach (var url in urls)
                {
                    try
                    {
                        var list = await _yts.GetPlaylistOrVideoAsync(url, CancellationToken.None);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => { foreach (var it in list) _queue.Add(it); });
                    }
                    catch { }
                }
            });
            if (_urlTextBoxCtrl != null) _urlTextBoxCtrl.Text = string.Empty;
        }

        private void OutputFormatCombo_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            // Update output format in settings when user changes selection
            var combo = sender as Avalonia.Controls.ComboBox;
            if (combo?.SelectedItem is Avalonia.Controls.ComboBoxItem item)
            {
                var fmt = item.Content?.ToString() ?? "mp3";
                _settings.Settings.OutputFormat = fmt;
                _settings.Save();
            }
        }

        private void SkipButton_Click(object? sender, RoutedEventArgs e)
        {
            _dm?.SkipCurrent();
            Log("Skipped current item");
        }

        private void QueueItem_Pause_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is YTP.Core.Models.VideoItem vi)
            {
                var cur = btn.Content?.ToString() ?? "Pause";
                if (cur == "Pause")
                {
                    _dm?.PauseItem(vi.Id);
                    btn.Content = "Resume";
                }
                else
                {
                    _dm?.ResumeItem(vi.Id);
                    btn.Content = "Pause";
                }
            }
        }

        private void QueueItem_Remove_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is YTP.Core.Models.VideoItem vi)
            {
                if (_dm != null && _dm.RemoveItem(vi.Id))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => { _queue.Remove(vi); });
                }
            }
        }
    }
}
