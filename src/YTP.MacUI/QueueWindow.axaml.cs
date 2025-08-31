using Avalonia.Controls;
using YTP.Core.Models;
using System.Collections.ObjectModel;

namespace YTP.MacUI
{
    public partial class QueueWindow : Window
    {
        public QueueWindow(ObservableCollection<VideoItem> source)
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            DataContext = source;
        }
    }
}
