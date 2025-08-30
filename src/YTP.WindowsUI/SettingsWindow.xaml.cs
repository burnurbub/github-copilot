using System.Windows;
using Wpf.Ui.Controls;
using YTP.Core.Settings;

namespace YTP.WindowsUI
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly SettingsManager _settings;

        public SettingsWindow(SettingsManager settings)
        {
            InitializeComponent();
            _settings = settings;

            // populate existing fields
            SkipLyricsCheck.IsChecked = _settings.Settings.SkipLyricsScrape;
            SkipAlbumArtCheck.IsChecked = _settings.Settings.SkipAlbumArt;
            FfmpegPathText.Text = _settings.Settings.FfmpegPath;
            OutputDirText.Text = _settings.Settings.OutputDirectory;

            // set min sizes for this window from settings
            this.MinWidth = Math.Max(this.MinWidth, _settings.Settings.SettingsWindowMinWidth);
            this.MinHeight = Math.Max(this.MinHeight, _settings.Settings.SettingsWindowMinHeight);

            // populate new theme/window fields
            ThemeCombo.SelectedIndex = _settings.Settings.ThemeMode switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0,
            };
            MatchSystemCheck.IsChecked = _settings.Settings.MatchSystemTheme;

            MainMinW.Text = ((int)_settings.Settings.MainWindowMinWidth).ToString();
            MainMinH.Text = ((int)_settings.Settings.MainWindowMinHeight).ToString();
            SettingsMinW.Text = ((int)_settings.Settings.SettingsWindowMinWidth).ToString();
            SettingsMinH.Text = ((int)_settings.Settings.SettingsWindowMinHeight).ToString();
        }

    private void ThemeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // preview theme safely
            var sel = ThemeCombo.SelectedIndex;
            if (MatchSystemCheck.IsChecked ?? false)
            {
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
                return;
            }

            switch (sel)
            {
                case 1:
            // Preview full theme including accent so buttons remain readable immediately
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, updateAccent: true);
                    break;
                case 2:
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark, updateAccent: true);
                    break;
                default:
                    Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
                    break;
            }
        }

        private void MatchSystemCheck_Checked(object sender, RoutedEventArgs e)
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
        }

        private void MatchSystemCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            // when user unchecks, re-apply the explicit selection
            ThemeCombo_SelectionChanged(null, null);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            _settings.Update(s =>
            {
                s.SkipLyricsScrape = SkipLyricsCheck.IsChecked ?? true;
                s.SkipAlbumArt = SkipAlbumArtCheck.IsChecked ?? false;
                s.FfmpegPath = FfmpegPathText.Text ?? string.Empty;
                s.OutputDirectory = OutputDirText.Text ?? s.OutputDirectory;

                // theme
                s.MatchSystemTheme = MatchSystemCheck.IsChecked ?? true;
                s.ThemeMode = ThemeCombo.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };

                // window sizing
                if (int.TryParse(MainMinW.Text, out var mmw)) s.MainWindowMinWidth = Math.Max(200, mmw);
                if (int.TryParse(MainMinH.Text, out var mmh)) s.MainWindowMinHeight = Math.Max(150, mmh);
                if (int.TryParse(SettingsMinW.Text, out var smw)) s.SettingsWindowMinWidth = Math.Max(300, smw);
                if (int.TryParse(SettingsMinH.Text, out var smh)) s.SettingsWindowMinHeight = Math.Max(200, smh);
            });

            // try to notify owner MainWindow to apply theme immediately
            if (this.Owner is MainWindow main)
            {
                // If user wants to match system, apply system theme with system accent
                if (_settings.Settings.MatchSystemTheme || _settings.Settings.ThemeMode == "System")
                {
                    Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(true);
                }
                else
                {
                    // apply explicit theme without updating accent (keep system accent)
                    if (_settings.Settings.ThemeMode == "Light")
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, updateAccent: false);
                    else if (_settings.Settings.ThemeMode == "Dark")
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark, updateAccent: false);
                }

                main.MinWidth = _settings.Settings.MainWindowMinWidth;
                main.MinHeight = _settings.Settings.MainWindowMinHeight;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void ChooseFfmpegBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Filter = "ffmpeg.exe|ffmpeg.exe|All files|*.*";
            dlg.FileName = FfmpegPathText.Text;
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                FfmpegPathText.Text = dlg.FileName;
            }
        }

        private void ChooseOutputBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = OutputDirText.Text;
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirText.Text = dlg.SelectedPath;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
