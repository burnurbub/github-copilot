using System;

namespace YTP.Core.Settings
{
    public class AppSettings
    {
        public string FfmpegPath { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string Mp3Quality { get; set; } = "320k";
        public string VideoQuality { get; set; } = "1080p";
    // By default don't scrape lyrics (disabled)
    public bool SkipLyricsScrape { get; set; } = true;
        public bool SkipAlbumArt { get; set; } = false;
        public bool ShowProgressBar { get; set; } = true;
    // Theme settings
    public string ThemeMode { get; set; } = "System"; // System, Light, Dark
    public bool MatchSystemTheme { get; set; } = true;
    public string AccentColor { get; set; } = string.Empty; // optional hex color

    // Window sizing
    public double MainWindowMinWidth { get; set; } = 700;
    public double MainWindowMinHeight { get; set; } = 400;
    public double SettingsWindowMinWidth { get; set; } = 400;
    public double SettingsWindowMinHeight { get; set; } = 300;
    // Output format (mp3 or mp4)
    public string OutputFormat { get; set; } = "mp3";
    }
}
