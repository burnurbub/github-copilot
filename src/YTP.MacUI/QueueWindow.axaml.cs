using Avalonia.Controls;
using YTP.Core.Models;
using System.Collections.ObjectModel;
using Avalonia.Interactivity;

namespace YTP.MacUI
{
    public partial class QueueWindow : Window
    {
    // second parameter: true => pause requested, false => resume requested
    public System.Action<string, bool>? OnPauseToggle { get; set; }
        public System.Action<string>? OnRemove { get; set; }

        public QueueWindow(ObservableCollection<VideoItem> source)
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            DataContext = source;
        }

        // parameterless ctor for XAML loader
        public QueueWindow()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            DataContext = new ObservableCollection<VideoItem>();
        }

        private void Pause_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is VideoItem vi)
            {
                // toggle the button content locally for immediate feedback
                var cur = btn.Content?.ToString() ?? "Pause";
                if (cur == "Pause")
                {
                    btn.Content = "Resume";
                    OnPauseToggle?.Invoke(vi.Id, true);
                }
                else
                {
                    btn.Content = "Pause";
                    OnPauseToggle?.Invoke(vi.Id, false);
                }
            }
        }

        private void Remove_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is VideoItem vi)
            {
                OnRemove?.Invoke(vi.Id);
            }
        }
    }
}
