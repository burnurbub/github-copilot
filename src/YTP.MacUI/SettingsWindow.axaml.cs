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
            if (this.FindControl<Avalonia.Controls.CheckBox>("AutoRetry403Check") is Avalonia.Controls.CheckBox ar) ar.IsChecked = _settings.Settings.AutoRetry403;
            if (this.FindControl<Avalonia.Controls.NumericUpDown>("MaxRetriesBox") is Avalonia.Controls.NumericUpDown mr) mr.Value = _settings.Settings.MaxRetries;
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<Avalonia.Controls.TextBox>("OutputDirBox") is Avalonia.Controls.TextBox od) _settings.Settings.OutputDirectory = od.Text ?? _settings.Settings.OutputDirectory;
            if (this.FindControl<Avalonia.Controls.TextBox>("FfmpegPathBox") is Avalonia.Controls.TextBox fb) _settings.Settings.FfmpegPath = fb.Text ?? _settings.Settings.FfmpegPath;
            if (this.FindControl<Avalonia.Controls.CheckBox>("AutoRetry403Check") is Avalonia.Controls.CheckBox arc) _settings.Settings.AutoRetry403 = arc.IsChecked ?? _settings.Settings.AutoRetry403;
            if (this.FindControl<Avalonia.Controls.NumericUpDown>("MaxRetriesBox") is Avalonia.Controls.NumericUpDown mrv) _settings.Settings.MaxRetries = (int)(mrv.Value ?? _settings.Settings.MaxRetries);
            _settings.Save();
            this.Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
