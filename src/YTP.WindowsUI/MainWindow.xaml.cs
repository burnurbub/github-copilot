using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using YTP.Core.Download;
using YTP.Core.Services;
using YTP.Core.Settings;
using System.Windows.Forms;
using Wpf.Ui;

namespace YTP.WindowsUI
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
    private System.Collections.ObjectModel.ObservableCollection<VideoItemViewModel> _queueItems = new();
        private DownloadManager? _dm;
        private CancellationTokenSource? _cts;
        private SettingsManager _settings;
    private Wpf.Ui.IContentDialogService? _contentDialogService;
    private DispatcherTimer _totalTimer;
    private DispatcherTimer _itemTimer;
    private DateTime? _totalStart;
    private DateTime? _itemStart;
    private int _currentIndex = 0;
    private int _totalItems = 0;
    private int _lastIndex = -1;
    private string? _currentItemId;
    // drag/drop helpers
    private Point _dragStartPoint;
    private object? _draggedItem;

        public MainWindow()
        {
            InitializeComponent();
            _settings = new SettingsManager();
            // ensure ContentDialog host is registered so ContentDialog.ShowAsync won't throw
            try
            {
                _contentDialogService = new Wpf.Ui.ContentDialogService();
                _contentDialogService.SetDialogHost(RootContentDialog);
            }
            catch { }
            OutputDirText.Text = _settings.Settings.OutputDirectory;

            // enforce minimum sizes from settings
            this.MinWidth = _settings.Settings.MainWindowMinWidth;
            this.MinHeight = _settings.Settings.MainWindowMinHeight;

            // apply theme preference
            ApplyThemeFromSettings();

            _totalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _totalTimer.Tick += (s, e) => {
                if (_totalStart.HasValue) TotalElapsedText.Text = (DateTime.Now - _totalStart.Value).ToString(@"hh\:mm\:ss");
            };

            _itemTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _itemTimer.Tick += (s, e) => {
                if (_itemStart.HasValue) ItemElapsedText.Text = (DateTime.Now - _itemStart.Value).ToString(@"hh\:mm\:ss");
            };
            // initialize output format from settings
            try
            {
                OutputFormatCombo.SelectedIndex = _settings.Settings.OutputFormat == "mp4" ? 1 : 0;
            }
            catch { OutputFormatCombo.SelectedIndex = 0; }
        }

        private void ChooseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Select output directory";
            dlg.SelectedPath = _settings.Settings.OutputDirectory ?? Environment.CurrentDirectory;
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                var path = dlg.SelectedPath;
                OutputDirText.Text = path;
                _settings.Update(s => s.OutputDirectory = path);
            }
        }

    // temporary lyrics download UI removed

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogText.AppendText(msg + "\n");
                LogText.ScrollToEnd();
            });
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var urlsRaw = UrlTextBox.Text;
            var urls = urlsRaw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToArray();
            if (urls.Length == 0)
            {
                var mb = new Wpf.Ui.Controls.MessageBox { Title = "Validation", PrimaryButtonText = "OK", CloseButtonText = "Close" };
                _ = mb.ShowDialogAsync();
                return;
            }

            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            AbortButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            var yts = new YoutubeExplodeService();
            var ff = new FFmpegService(_settings.Settings.FfmpegPath);
            // pass MetadataService wired with settings so it respects SkipLyricsScrape
            _dm = new DownloadManager(yts, ff, _settings.Settings.OutputDirectory, (f, _) => new YoutubeDownloaderService(f, new YTP.Core.Services.MetadataService(_settings)));
            _dm.LogMessage += s => Log(s);
            _dm.ProgressChanged += p => {
                // Ensure UI thread updates without blocking the background worker
                Dispatcher.BeginInvoke(() => {
                // start total timer when first progress arrives
                    if (!_totalStart.HasValue)
                    {
                        _totalStart = DateTime.Now;
                        _totalTimer.Start();
                    }

                    // update counts
                    _totalItems = p.TotalItems > 0 ? p.TotalItems : _totalItems;
                    _currentIndex = p.CurrentIndex > 0 ? p.CurrentIndex : _currentIndex;
                    PlaylistInfoText.Text = _totalItems > 0 ? $"{_currentIndex}/{_totalItems}" : string.Empty;
                    CurrentSongText.Text = p.Item?.Title ?? string.Empty;

                    // update progress bar
                    ItemProgress.Value = Math.Max(0, Math.Min(1, p.Percentage));

                    // start/refresh per-item timer when the song index changes
                    if (!_itemStart.HasValue || p.CurrentIndex != _lastIndex)
                    {
                        _itemStart = DateTime.Now;
                        ItemElapsedText.Text = "00:00:00";
                        _itemTimer.Start();

                        // Track current item id and honor any per-item pause state
                        _currentItemId = p.Item?.Id;
                        if (!string.IsNullOrEmpty(_currentItemId))
                        {
                            var v = _queueItems.FirstOrDefault(x => x.Id == _currentItemId);
                            if (v != null && v.IsPaused)
                            {
                                // ensure downloader is paused when entering a paused item
                                _dm?.Pause();
                                PauseButton.Content = "Resume";
                                PauseButton.IsEnabled = true;
                            }
                            else
                            {
                                // ensure downloader is resumed for unpaused items
                                _dm?.Resume();
                                PauseButton.Content = "Pause";
                                PauseButton.IsEnabled = true;
                            }
                        }
                    }

                    // update elapsed displays
                    ItemElapsedText.Text = p.ItemElapsed.ToString(@"hh\:mm\:ss");
                    TotalElapsedText.Text = p.TotalElapsed.ToString(@"hh\:mm\:ss");

                    // stop timers when complete
                    if (p.Percentage >= 1.0 && p.TotalItems > 0 && p.CurrentIndex == p.TotalItems)
                    {
                        _itemTimer.Stop();
                        _totalTimer.Stop();
                    }

                    _lastIndex = p.CurrentIndex;
                });
            };

            // bind queue UI
            QueueList.ItemsSource = _queueItems;

            // populate queue items by fetching metadata for URLs (UI-friendly titles)
            _queueItems.Clear();
            foreach (var url in urls)
            {
                try
                {
                    var list = await yts.GetPlaylistOrVideoAsync(url, _cts.Token);
                    foreach (var vi in list)
                    {
                        _queueItems.Add(new VideoItemViewModel(vi));
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to retrieve info for {url}: {ex.Message}");
                }
            }

            try
            {
                await _dm.DownloadQueueAsync(urls, _cts.Token);
                var dialog = new Wpf.Ui.Controls.ContentDialog()
                {
                    Title = "Complete",
                    Content = "Downloads complete.",
                    PrimaryButtonText = "Open Folder",
                    CloseButtonText = "Close"
                };
                // Use the instance content dialog service so the DialogHost is applied
                var res = _contentDialogService != null
                    ? await _contentDialogService.ShowAsync(dialog, CancellationToken.None)
                    : await dialog.ShowAsync();
                if (res == Wpf.Ui.Controls.ContentDialogResult.Primary)
                {
                    OpenDownloadedFolder();
                }
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
                StartButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                PauseButton.Content = "Pause";
                AbortButton.IsEnabled = false;
                ItemProgress.Value = 0;
                // stop timers
                _itemTimer.Stop();
                _totalTimer.Stop();
                _itemStart = null;
                _totalStart = null;
            }
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            AbortButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            PauseButton.Content = "Pause";
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle pause/unpause
            if (_dm != null)
            {
                if (PauseButton.Content?.ToString() == "Pause")
                {
                    _dm.Pause();
                    PauseButton.Content = "Resume";
                    Log("Paused");
                }
                else
                {
                    _dm.Resume();
                    PauseButton.Content = "Pause";
                    Log("Resumed");
                }
            }
        }

        private void QueueItem_Pause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is VideoItemViewModel vvm)
            {
                // toggle per-item paused state
                vvm.IsPaused = !vvm.IsPaused;
                Log($"Queue item {(vvm.IsPaused ? "paused" : "resumed")}: {vvm.Title}");

                // if this is the currently downloading item, toggle the global downloader
                if (!string.IsNullOrEmpty(_currentItemId) && vvm.Id == _currentItemId)
                {
                    if (vvm.IsPaused)
                    {
                        _dm?.Pause();
                        PauseButton.Content = "Resume";
                        PauseButton.IsEnabled = true;
                    }
                    else
                    {
                        _dm?.Resume();
                        PauseButton.Content = "Pause";
                        PauseButton.IsEnabled = true;
                    }
                }
            }
        }

        private void QueueItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is VideoItemViewModel vvm)
            {
                _queueItems.Remove(vvm);
                Log($"Removed from queue: {vvm.Title}");
            }
        }

        private void QueueList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var container = (System.Windows.Controls.Primitives.UniformGrid?)null;
            // find the item under mouse
            var element = e.OriginalSource as DependencyObject;
            while (element != null && !(element is System.Windows.Controls.ListBoxItem) && !(element is System.Windows.Controls.ContentPresenter))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            if (element is FrameworkElement fe && fe.DataContext != null)
            {
                _draggedItem = fe.DataContext;
            }
        }

        private void QueueList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedItem != null)
            {
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var data = new System.Windows.DataObject("VideoItem", _draggedItem);
                    System.Windows.DragDrop.DoDragDrop(this, data, System.Windows.DragDropEffects.Move);
                }
            }
        }

    private void QueueList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("VideoItem"))
            {
                var dropped = e.Data.GetData("VideoItem") as VideoItemViewModel ?? (e.Data.GetData("VideoItem") as YTP.Core.Models.VideoItem) switch
                {
                    YTP.Core.Models.VideoItem vi => new VideoItemViewModel(vi),
                    _ => null
                };
                if (dropped == null) return;

                // Determine target index
                var pos = e.GetPosition(QueueList);
                int index = 0;
                for (int i = 0; i < QueueList.Items.Count; i++)
                {
                    var item = (FrameworkElement)QueueList.ItemContainerGenerator.ContainerFromIndex(i);
                    if (item == null) continue;
                    var bounds = new Rect(item.TranslatePoint(new Point(0, 0), QueueList), new Size(item.ActualWidth, item.ActualHeight));
                    if (pos.Y < bounds.Bottom)
                    {
                        index = i;
                        break;
                    }
                    index = i + 1;
                }

                var oldIndex = _queueItems.ToList().FindIndex(x => x.Id == dropped.Id);
                if (oldIndex >= 0)
                {
                    if (oldIndex < index) index--;
                    _queueItems.Move(oldIndex, index);
                }
            }
            _draggedItem = null;
        }

        // Clear-URLs popup handlers
        private void ClearUrlsButton_Click(object sender, RoutedEventArgs e)
        {
            ClearConfirmPopup.IsOpen = true;
        }

        private void ClearConfirmYes_Click(object sender, RoutedEventArgs e)
        {
            UrlTextBox.Clear();
            ClearConfirmPopup.IsOpen = false;
        }

        private void ClearConfirmNo_Click(object sender, RoutedEventArgs e)
        {
            ClearConfirmPopup.IsOpen = false;
        }

        private void OpenDownloadedFolder()
        {
            var path = OutputDirText.Text;
            if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = path,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch { }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new SettingsWindow(_settings) { Owner = this };
            var res = wnd.ShowDialog();
            if (res == true)
            {
                // refresh output dir text
                OutputDirText.Text = _settings.Settings.OutputDirectory;
            }
        }

    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var path = OutputDirText.Text;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
            {
                var mf = new Wpf.Ui.Controls.MessageBox { Title = "Open Folder", PrimaryButtonText = "OK", CloseButtonText = "Close" };
        _ = await mf.ShowDialogAsync();
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        catch (Exception)
            {
                var me = new Wpf.Ui.Controls.MessageBox { Title = "Open Folder", PrimaryButtonText = "OK", CloseButtonText = "Close" };
                _ = await me.ShowDialogAsync();
            }
        }

        private async void PasteClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    var text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (!string.IsNullOrEmpty(UrlTextBox.Text) && !UrlTextBox.Text.EndsWith("\n"))
                            UrlTextBox.AppendText("\n");
                        UrlTextBox.AppendText(text.Trim());
                    }
                }
            }
            catch (Exception)
            {
                var mp = new Wpf.Ui.Controls.MessageBox { Title = "Paste", PrimaryButtonText = "OK", CloseButtonText = "Close" };
                _ = await mp.ShowDialogAsync();
            }
        }

    // older MessageBox-based clear handler removed; replaced with popup flyout handlers above

        private void OutputFormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var item = OutputFormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (item != null)
            {
                var fmt = item.Content?.ToString();
                if (!string.IsNullOrEmpty(fmt))
                {
                    try
                    {
                        _settings.Update(s => s.OutputFormat = fmt);
                    }
                    catch { }
                }
            }
        }

        public void ApplyThemeFromSettings()
        {
            try
            {
                var mode = _settings.Settings.ThemeMode ?? "System";

                // If user wants to match system or explicitly selected System, apply system theme (includes system accent)
                if (_settings.Settings.MatchSystemTheme || mode == "System")
                {
                            Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
                    return;
                }

                // Explicit themes: apply theme but do not update accent (keep system accent)
                switch (mode)
                {
                    case "Light":
                        // apply explicit theme and keep system accent
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, updateAccent: true);
                        break;
                    case "Dark":
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark, updateAccent: true);
                        break;
                    default:
                        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
                        break;
                }
            }
            catch { }
        }

        // lightweight wrapper for per-item UI state
        private class VideoItemViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            public YTP.Core.Models.VideoItem Inner { get; }
            public string Id => Inner?.Id ?? Guid.NewGuid().ToString();
            public string Title => Inner?.Title ?? string.Empty;
            private bool _isPaused;
            public bool IsPaused
            {
                get => _isPaused;
                set
                {
                    if (_isPaused != value)
                    {
                        _isPaused = value;
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPaused)));
                    }
                }
            }

            public VideoItemViewModel(YTP.Core.Models.VideoItem vi)
            {
                Inner = vi ?? throw new ArgumentNullException(nameof(vi));
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }
    }
}

// Converter for binding a bool IsPaused to a Symbol enum for SymbolIcon
namespace YTP.WindowsUI
{
    public class BoolToSymbolConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var isPaused = false;
            if (value is bool b) isPaused = b;
            // show Play icon when paused, Pause icon when playing
            return isPaused ? Wpf.Ui.Controls.SymbolRegular.Play12 : Wpf.Ui.Controls.SymbolRegular.Pause12;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Wpf.Ui.Controls.SymbolRegular s)
            {
                return s == Wpf.Ui.Controls.SymbolRegular.Play24;
            }
            return false;
        }
    }
}
