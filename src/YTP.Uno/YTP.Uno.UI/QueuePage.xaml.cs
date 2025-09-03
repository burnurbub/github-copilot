using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using YTP.Core.Models;
using YTP.Core.Download;

namespace YTP.Uno.UI
{
    public sealed partial class QueuePage : Page
    {
        public ObservableCollection<VideoItem> Items { get; private set; } = new ObservableCollection<VideoItem>();
        private DownloadManager? _dm;

        // parameterless ctor used by the navigation system
        public QueuePage()
        {
            this.InitializeComponent();
        }

        // set the runtime context after navigation
        public void SetContext(ObservableCollection<VideoItem> items, DownloadManager? dm)
        {
            Items = items ?? new ObservableCollection<VideoItem>();
            _dm = dm;
            QueueListView.ItemsSource = Items;
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string id)
            {
                // toggle using DownloadManager per-item pause/resume
                _dm?.PauseItem(id);
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string id)
            {
                _dm?.RemoveItem(id);
                var item = FindItemById(id);
                if (item != null) Items.Remove(item);
            }
        }

        private VideoItem? FindItemById(string id)
        {
            foreach (var it in Items)
            {
                if (it.Id == id) return it;
            }
            return null;
        }
    }
}
