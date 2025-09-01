using Avalonia.Controls;
using Avalonia.Interactivity;
using YTP.Core.Settings;

namespace YTP.MacUI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settings = new();
        public SettingsWindow()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            if (this.FindControl<Avalonia.Controls.TextBox>("OutputDirBox") is Avalonia.Controls.TextBox od) od.Text = _settings.Settings.OutputDirectory;
            if (this.FindControl<Avalonia.Controls.TextBox>("FfmpegPathBox") is Avalonia.Controls.TextBox fb) fb.Text = _settings.Settings.FfmpegPath;
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<Avalonia.Controls.TextBox>("OutputDirBox") is Avalonia.Controls.TextBox od) _settings.Settings.OutputDirectory = od.Text ?? _settings.Settings.OutputDirectory;
            if (this.FindControl<Avalonia.Controls.TextBox>("FfmpegPathBox") is Avalonia.Controls.TextBox fb) _settings.Settings.FfmpegPath = fb.Text ?? _settings.Settings.FfmpegPath;
            _settings.Save();
            this.Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
