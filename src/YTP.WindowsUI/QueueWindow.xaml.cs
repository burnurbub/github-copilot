using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Collections.ObjectModel;

namespace YTP.WindowsUI
{
    public partial class QueueWindow : Wpf.Ui.Controls.FluentWindow
    {
    // Routed commands for keyboard shortcuts
    public static readonly RoutedUICommand PauseCommand = new("Pause", "Pause", typeof(QueueWindow));
    public static readonly RoutedUICommand RemoveCommand = new("Remove", "Remove", typeof(QueueWindow));
    public static readonly RoutedUICommand ExpandAllCommand = new("ExpandAll", "ExpandAll", typeof(QueueWindow));

        public System.Collections.ObjectModel.ObservableCollection<object> Queue { get; private set; }
        public System.Action<string>? OnPauseToggle { get; set; }
        public System.Action<string>? OnRemove { get; set; }
    public System.Action<string>? OnOpenLink { get; set; }
    public System.Action<string>? OnCopyLink { get; set; }
    public System.Action<string>? OnShowInExplorer { get; set; }

        private System.Collections.Specialized.INotifyCollectionChanged? _sourceNotifier;
        private System.Collections.ObjectModel.ObservableCollection<MainWindow.VideoItemViewModel>? _sourceCollection;

        public QueueWindow(System.Collections.ObjectModel.ObservableCollection<MainWindow.VideoItemViewModel> queue)
        {
            InitializeComponent();
            // Input bindings for keyboard shortcuts
            this.InputBindings.Add(new System.Windows.Input.KeyBinding(PauseCommand, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.Control));
            this.InputBindings.Add(new System.Windows.Input.KeyBinding(RemoveCommand, System.Windows.Input.Key.Delete, System.Windows.Input.ModifierKeys.None));
            this.InputBindings.Add(new System.Windows.Input.KeyBinding(ExpandAllCommand, System.Windows.Input.Key.E, System.Windows.Input.ModifierKeys.Control));

            CommandBindings.Add(new System.Windows.Input.CommandBinding(PauseCommand, (s,e)=> ExecutePauseOnFocusedItem(), (s,e)=> e.CanExecute = true));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(RemoveCommand, (s,e)=> ExecuteRemoveOnFocusedItem(), (s,e)=> e.CanExecute = true));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(ExpandAllCommand, (s,e)=> ExpandAll(), (s,e)=> e.CanExecute = true));
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
                // Build a simple grouping by PlaylistTitle
                var groups = _sourceCollection.GroupBy(v => string.IsNullOrWhiteSpace(v.Inner.PlaylistTitle) ? "(single)" : v.Inner.PlaylistTitle);
                Queue.Clear();
                foreach (var g in groups)
                {
                    var node = new PlaylistNode { Title = g.Key ?? "Playlist" };
                    int idx = 1;
                    foreach (var item in g)
                    {
                        node.Items.Add(new QueueItemNode { Id = item.Id, DisplayTitle = item.Title, Subtitle = item.Inner.Channel ?? string.Empty, InnerVm = item, Index = idx });
                        idx++;
                    }
                    Queue.Add(node);
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

        private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v && v.InnerVm != null)
            {
                OnOpenLink?.Invoke(v.Id);
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v && v.InnerVm != null)
            {
                OnCopyLink?.Invoke(v.Id);
            }
        }

        private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v && v.InnerVm != null)
            {
                OnShowInExplorer?.Invoke(v.Id);
            }
        }

        private void ExecutePauseOnFocusedItem()
        {
            var focused = Keyboard.FocusedElement as FrameworkElement;
            if (focused?.DataContext is QueueItemNode q) OnPauseToggle?.Invoke(q.Id);
        }

        private void ExecuteRemoveOnFocusedItem()
        {
            var focused = Keyboard.FocusedElement as FrameworkElement;
            if (focused?.DataContext is QueueItemNode q) OnRemove?.Invoke(q.Id);
        }

        private void ExpandAll()
        {
            foreach (var o in Queue)
            {
                if (o is PlaylistNode pn)
                {
                    // Find Expander in visual tree by matching header title - simple approach: toggle via code-behind not implemented; leave placeholder
                }
            }
        }

    private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is QueueItemNode v)
            {
                // remove from parent collection
                // find and remove node in Queue
                object? toRemove = null;
                foreach (var o in Queue)
                {
                    if (o is PlaylistNode pn)
                    {
                        var found = pn.Items.FirstOrDefault(x => x.Id == v.Id);
                        if (found != null) { pn.Items.Remove(found); toRemove = null; break; }
                    }
                    else if (o is QueueItemNode qn && qn.Id == v.Id)
                    {
                        toRemove = o; break;
                    }
                }
                if (toRemove != null) Queue.Remove(toRemove);
                OnRemove?.Invoke(v.Id);
            }
        }
        private void ActionButton_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // prevent parent drag/selection from starting when interacting with small action buttons
            e.Handled = true;
        }

        private void Item_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ensure clicked item gets focus for keyboard shortcuts
            if (sender is FrameworkElement fe)
            {
                fe.Focus();
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
        public int Index { get; set; } = 0;
        public Wpf.Ui.Controls.SymbolRegular IconSymbol => InnerVm != null && InnerVm.IsPaused ? Wpf.Ui.Controls.SymbolRegular.Play12 : Wpf.Ui.Controls.SymbolRegular.Pause12;
    }

} 
