using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using YTP.Core.Download;
using YTP.Core.Settings;
using YTP.Core.Services;

namespace YTP.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private DownloadManager? _dm;
        private CancellationTokenSource? _cts;
        private SettingsManager _settings;

        public MainWindow()
        {
            this.InitializeComponent();
            _settings = new SettingsManager();
            OutputDirText.Text = _settings.Settings.OutputDirectory;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire download logic similar to WPF MainWindow
        }
    }
}
