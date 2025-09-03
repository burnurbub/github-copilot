using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using YTP.Core;
using YTP.Core.Models;
using YTP.Core.Services;
using YTP.Core.Download;

namespace YTP.Uno.UI
{
    public partial class MainPage : Page
    {
    private DownloadManager? _dm;
    private CancellationTokenSource? _cts;
    private ObservableCollection<VideoItem> _queue = new ObservableCollection<VideoItem>();

    public ObservableCollection<VideoItem> Queue => _queue;

        public MainPage()
        {
            this.InitializeComponent();
            QueueSummary.Text = "Queue: 0 items";
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dm == null)
            {
                // instantiate core services and DownloadManager
                var yts = new YoutubeExplodeService();
                var ffmpeg = new FFmpegService("ffmpeg");
                var outputDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                _dm = new DownloadManager(yts, ffmpeg, outputDir);
                _dm.ProgressChanged += Dm_ProgressChanged;
                _dm.LogMessage += Dm_LogMessage;
            }

            _cts = new CancellationTokenSource();
            // run download loop without capturing the UI context
            await Task.Run(() => _dm.DownloadItemsAsync(_queue, _cts.Token));
        }

        private void Dm_LogMessage(object? sender, string e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                LogText.Text += e + "\n";
            });
        }

        private void Dm_ProgressChanged(object? sender, DownloadProgress e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e != null)
                {
                    ItemProgress.Value = e.Percentage * 100.0;
                    QueueSummary.Text = $"Queue: {e.TotalItems} items (#{e.CurrentIndex})";
                }
            });
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dm != null)
            {
                _dm.Pause();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            _dm?.SkipCurrent();
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private async void EnqueueButton_Click(object sender, RoutedEventArgs e)
        {
            var text = UrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // naive: treat as single video URL
            var vi = new VideoItem { Url = text, Title = text };
            _queue.Add(vi);
            QueueSummary.Text = $"Queue: {_queue.Count} items";

            await Task.CompletedTask;
        }

        private void OpenQueueButton_Click(object sender, RoutedEventArgs e)
        {
            // If Frame navigation is available, navigate and then set context on the created page
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(QueuePage), null);
                // attempt to get the page instance from the Frame and set context
                if (this.Frame.Content is QueuePage qp)
                {
                    qp.SetContext(_queue, _dm);
                }
            }
            else
            {
                var page = new QueuePage();
                page.SetContext(_queue, _dm);
                NavigationService?.Navigate(page);
            }
        }

        private void ClearUrlsButton_Click(object sender, RoutedEventArgs e)
        {
            UrlTextBox.Text = string.Empty;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null) this.Frame.Navigate(typeof(SettingsPage));
            else NavigationService?.Navigate(new SettingsPage());
        }

        private async void PasteClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (data != null && data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    var text = await data.GetTextAsync();
                    UrlTextBox.Text = text;
                }
            }
            catch
            {
                // no-op on platforms without clipboard access
            }
        }
    }
}
