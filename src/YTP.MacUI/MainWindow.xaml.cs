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

namespace YTP.MacUI
{
    public partial class MainWindow : Window
    {
    private readonly SettingsManager _settings = new();
        private DownloadManager? _dm;
        private FFmpegService? _ffmpeg;
        private IYoutubeService? _yts;
        private CancellationTokenSource? _cts;

        // simple observable queue for UI
        public ObservableCollection<VideoItem> Queue { get; } = new();

        public MainWindow()
        {
            // load XAML
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            DataContext = this;
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: show settings dialog - placeholder
        }

        private void OpenQueueButton_Click(object? sender, RoutedEventArgs e)
        {
            var qw = new QueueWindow(Queue);
            qw.Show();
        }

        private async void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            var tb = this.FindControl<Avalonia.Controls.TextBox>("UrlTextBox");
            var text = tb?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            // support multiple lines
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // ensure services
            if (_yts == null) _yts = new YoutubeExplodeService();
            if (_ffmpeg == null) _ffmpeg = new FFmpegService(_settings.Settings.FfmpegPath);
            if (_dm == null) _dm = new DownloadManager(_yts, _ffmpeg, _settings.Settings.OutputDirectory);

            // fetch items for each URL and append to queue
            foreach (var line in lines)
            {
                try
                {
                    var list = await _yts.GetPlaylistOrVideoAsync(line, CancellationToken.None);
                    foreach (var it in list) Queue.Add(it);
                }
                catch (Exception ex)
                {
                    // ignore errors for now
                }
            }

            // clear input
            if (tb != null) tb.Text = string.Empty;

            // start download if not running
            if (_cts == null)
            {
                _cts = new CancellationTokenSource();
                _ = Task.Run(async () => {
                    try
                    {
                        // create fresh list snapshot
                        while (true)
                        {
                            VideoItem[] snapshot;
                            lock (Queue)
                            {
                                if (Queue.Count == 0) break;
                                snapshot = new VideoItem[Queue.Count];
                                Queue.CopyTo(snapshot, 0);
                                Queue.Clear();
                            }
                            await _dm!.DownloadItemsAsync(snapshot, _cts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception) { }
                    finally { _cts = null; }
                });
            }
        }
    }
}
