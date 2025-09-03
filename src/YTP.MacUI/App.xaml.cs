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
                    }
                    else
                    {
                        this.Resources["WindowBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F2F2F7"));
                        this.Resources["CardBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
                        this.Resources["SubtleTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6E6E73"));
                    }

                    // Try to read macOS accent color via `defaults read -g AppleAccentColor`
                    try
                    {
                        var p2 = new System.Diagnostics.ProcessStartInfo { FileName = "/usr/bin/defaults", Arguments = "read -g AppleAccentColor", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                        using var proc2 = System.Diagnostics.Process.Start(p2);
                        if (proc2 != null)
                        {
                            var outp2 = proc2.StandardOutput.ReadToEnd().Trim();
                            proc2.WaitForExit(250);
                            if (int.TryParse(outp2, out var accentCode))
                            {
                                // Map common accent codes to hex colors (approximate)
                                var accent = accentCode switch
                                {
                                    1 => "#AF52DE", // purple
                                    2 => "#FF375F", // pink/red
                                    3 => "#FF3B30", // red
                                    4 => "#FF9500", // orange
                                    5 => "#0A84FF", // treat yellow code as system blue to avoid poor contrast
                                    6 => "#34C759", // green
                                    7 => "#8E8E93", // graphite
                                    _ => "#0A84FF", // blue/default (HIG)
                                };
                                this.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(accent));
                            }
                        }
                    }
                    catch { /* ignore accent detection failures */ }
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
