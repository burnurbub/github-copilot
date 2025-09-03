using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace YTP.MacUI
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Detect macOS appearance (light/dark) and accent color where possible and apply to resources.
                try
                {
                    // Default brushes
                    var res = this.Resources;
                    // Determine dark mode by calling `defaults read -g AppleInterfaceStyle` which prints "Dark" when dark mode is enabled.
                    var isDark = false;
                    try
                    {
                        var p = new System.Diagnostics.ProcessStartInfo { FileName = "/usr/bin/defaults", Arguments = "read -g AppleInterfaceStyle", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                        using var proc = System.Diagnostics.Process.Start(p);
                        if (proc != null)
                        {
                            var outp = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(250);
                            if (!string.IsNullOrWhiteSpace(outp) && outp.Trim().ToLowerInvariant().Contains("dark")) isDark = true;
                        }
                    }
                    catch { }

                    // Apply basic light/dark background
                    if (isDark)
                    {
                        // neutral dark gray similar to macOS dark windows
                        this.Resources["WindowBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1C1C1E"));
                        this.Resources["CardBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#242426"));
                        this.Resources["SubtleTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#9FA0A3"));
                        // darker translucent panel and light log foreground for dark mode
                        this.Resources["TranslucentPanel"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#000000")) { Opacity = 0.18 };
                        this.Resources["LogForegroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F2F2F5"));
                    }
                    else
                    {
                        this.Resources["WindowBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F2F2F7"));
                        this.Resources["CardBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
                        this.Resources["SubtleTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6E6E73"));
                        this.Resources["TranslucentPanel"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF")) { Opacity = 0.86 };
                        this.Resources["LogForegroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1C1C1E"));
                    }

                    // For now, use the user's requested macOS purple accent to ensure consistent visuals.
                    this.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AF52DE"));
                }
                catch { }

                // Ensure AccentBrush exists as a fallback
                if (!this.Resources.ContainsKey("AccentBrush"))
                {
                    this.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0A84FF"));
                }

                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
