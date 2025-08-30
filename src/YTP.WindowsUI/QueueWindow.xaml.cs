using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Collections.ObjectModel;

namespace YTP.WindowsUI
{
    public partial class QueueWindow : Wpf.Ui.Controls.FluentWindow
    {
        public System.Collections.ObjectModel.ObservableCollection<object> Queue { get; private set; }
        public System.Action<string>? OnPauseToggle { get; set; }
        public System.Action<string>? OnRemove { get; set; }

        private System.Collections.Specialized.INotifyCollectionChanged? _sourceNotifier;
        private System.Collections.ObjectModel.ObservableCollection<MainWindow.VideoItemViewModel>? _sourceCollection;

        public QueueWindow(System.Collections.ObjectModel.ObservableCollection<MainWindow.VideoItemViewModel> queue)
        {
            InitializeComponent();
            // convert flat viewmodels to hierarchical nodes: playlists and standalone items
            var root = new System.Collections.ObjectModel.ObservableCollection<object>();
            var byPlaylist = queue.Where(i => i.Inner.IsPlaylistItem).GroupBy(i => i.Inner.PlaylistTitle ?? "(Unknown playlist)");
            foreach (var g in byPlaylist)
            {
                var node = new PlaylistNode { Title = g.Key ?? "Playlist" };
                foreach (var it in g)
                {
                    node.Items.Add(new QueueItemNode { Id = it.Id, DisplayTitle = it.Title, Subtitle = it.Inner.Channel ?? string.Empty, InnerVm = it });
                }
                root.Add(node);
            }
            // add single items
            var singles = queue.Where(i => !i.Inner.IsPlaylistItem);
            foreach (var s in singles)
            {
                root.Add(new QueueItemNode { Id = s.Id, DisplayTitle = s.Title, Subtitle = s.Inner.Channel ?? string.Empty, InnerVm = s });
            }
            Queue = root;
            this.DataContext = Queue;
        }

        /// <summary>
        /// Attach a live source collection. The window will subscribe to CollectionChanged and
        /// rebuild the tree when items change.
        /// </summary>
        public void SetSource(System.Collections.ObjectModel.ObservableCollection<MainWindow.VideoItemViewModel> source)
        {
            if (_sourceNotifier != null)
            {
                _sourceNotifier.CollectionChanged -= Source_CollectionChanged;
            }
            _sourceCollection = source;
            _sourceNotifier = source as System.Collections.Specialized.INotifyCollectionChanged;
            if (_sourceNotifier != null)
            {
                _sourceNotifier.CollectionChanged += Source_CollectionChanged;
            }
            RefreshFromSource();
        }

        private void Source_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Ensure we refresh on UI thread
            this.Dispatcher?.BeginInvoke((Action)(() => RefreshFromSource()));
        }

        /// <summary>
        /// Rebuilds the tree nodes from the current source collection.
        /// </summary>
        public void RefreshFromSource()
        {
            try
            {
                if (_sourceCollection == null) return;
                // Build grouping by PlaylistTitle
                var groups = _sourceCollection.GroupBy(v => string.IsNullOrWhiteSpace(v.Inner.PlaylistTitle) ? "(single)" : v.Inner.PlaylistTitle);

                // Create or update playlist nodes incrementally to avoid collapsing UI
                var existingPlaylists = Queue.OfType<PlaylistNode>().ToDictionary(p => p.Title ?? string.Empty, p => p);

                foreach (var g in groups)
                {
                    var title = g.Key ?? string.Empty;
                    if (!existingPlaylists.TryGetValue(title, out var playlistNode))
                    {
                        playlistNode = new PlaylistNode { Title = title };
                        Queue.Add(playlistNode);
                        existingPlaylists[title] = playlistNode;
                    }

                    // Sync items inside playlistNode: keep order from source
                    var desired = g.Select(item => item.Id).ToList();
                    // remove nodes not in desired
                    for (int i = playlistNode.Items.Count - 1; i >= 0; i--)
                    {
                        if (!desired.Contains(playlistNode.Items[i].Id)) playlistNode.Items.RemoveAt(i);
                    }
                    // add or reorder
                    for (int idx = 0; idx < desired.Count; idx++)
                    {
                        var id = desired[idx];
                        var existing = playlistNode.Items.FirstOrDefault(x => x.Id == id);
                        if (existing == null)
                        {
                            var srcVm = _sourceCollection.FirstOrDefault(x => x.Id == id);
                            if (srcVm != null)
                            {
                                var newNode = new QueueItemNode { Id = srcVm.Id, DisplayTitle = srcVm.Title, Subtitle = srcVm.Inner.Channel ?? string.Empty, InnerVm = srcVm };
                                playlistNode.Items.Insert(idx, newNode);
                            }
                        }
                        else
                        {
                            var currentIdx = playlistNode.Items.IndexOf(existing);
                            if (currentIdx != idx) playlistNode.Items.Move(currentIdx, idx);
                        }
                    }
                }
                // remove playlists that no longer exist
                for (int i = Queue.Count - 1; i >= 0; i--)
                {
                    if (Queue[i] is PlaylistNode pn)
                    {
                        if (!groups.Any(g => (g.Key ?? string.Empty) == (pn.Title ?? string.Empty)))
                            Queue.RemoveAt(i);
                    }
                }
            }
            catch { }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v)
            {
                if (v.InnerVm != null)
                {
                    v.InnerVm.IsPaused = !v.InnerVm.IsPaused;
                    OnPauseToggle?.Invoke(v.Id);
                }
            }
        }

    private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v)
            {
                // remove from underlying source collection so main window updates
                if (_sourceCollection != null)
                {
                    var vm = _sourceCollection.FirstOrDefault(x => x.Id == v.Id);
                    if (vm != null) _sourceCollection.Remove(vm);
                }
                // update local presentation immediately
                RefreshFromSource();
                OnRemove?.Invoke(v.Id);
            }
        }

        // Drag/drop: start drag when mouse moves
        private void TreeView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var tv = sender as TreeView;
            var src = e.OriginalSource as DependencyObject;
            var tvi = FindAncestor<System.Windows.Controls.TreeViewItem>(src);
            if (tvi != null && tvi.DataContext is QueueItemNode qn)
            {
                DragDrop.DoDragDrop(tvi, qn.Id, DragDropEffects.Move);
            }
        }

        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)))
            {
                var id = e.Data.GetData(typeof(string)) as string;
                var pos = e.OriginalSource as DependencyObject;
                var targetTvi = FindAncestor<System.Windows.Controls.TreeViewItem>(pos);
                string? targetId = null;
                if (targetTvi != null && targetTvi.DataContext is QueueItemNode qnTarget) targetId = qnTarget.Id;

                if (!string.IsNullOrEmpty(id) && _sourceCollection != null)
                {
                    var moving = _sourceCollection.FirstOrDefault(x => x.Id == id);
                    if (moving != null)
                    {
                        // remove and re-insert at target position
                        _sourceCollection.Remove(moving);
                        if (!string.IsNullOrEmpty(targetId))
                        {
                            var idx = _sourceCollection.ToList().FindIndex(x => x.Id == targetId);
                            if (idx >= 0) _sourceCollection.Insert(idx, moving); else _sourceCollection.Add(moving);
                        }
                        else
                        {
                            _sourceCollection.Add(moving);
                        }
                        RefreshFromSource();
                    }
                }
            }
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        // utility: find ancestor of a specific type
        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // Helpers to get/set expansion on tree nodes by title (simple approach)
        private bool GetIsExpanded(string title)
        {
            // find matching TreeViewItem and check IsExpanded
            foreach (var obj in Queue)
            {
                if (obj is PlaylistNode pn && (pn.Title ?? string.Empty) == title)
                {
                    // try to find the container
                    var cont = QueueTree.ItemContainerGenerator.ContainerFromItem(pn) as TreeViewItem;
                    if (cont != null) return cont.IsExpanded;
                }
            }
            return false;
        }

        private void ExpandPlaylist(string title)
        {
            foreach (var obj in Queue)
            {
                if (obj is PlaylistNode pn && (pn.Title ?? string.Empty) == title)
                {
                    var cont = QueueTree.ItemContainerGenerator.ContainerFromItem(pn) as TreeViewItem;
                    if (cont != null) cont.IsExpanded = true;
                }
            }
        }
    }

    // helper node types for TreeView (top-level so XAML can resolve them via the local namespace)
    public class PlaylistNode
    {
        public string Title { get; set; } = string.Empty;
        public ObservableCollection<QueueItemNode> Items { get; } = new();
    }

    public class QueueItemNode
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayTitle { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public MainWindow.VideoItemViewModel? InnerVm { get; set; }
        public Wpf.Ui.Controls.SymbolRegular IconSymbol => InnerVm != null && InnerVm.IsPaused ? Wpf.Ui.Controls.SymbolRegular.Play12 : Wpf.Ui.Controls.SymbolRegular.Pause12;
    }

}
