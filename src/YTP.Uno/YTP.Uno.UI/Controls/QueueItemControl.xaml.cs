using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace YTP.Uno.UI.Controls
{
    public sealed partial class QueueItemControl : UserControl
    {
        public string ItemId { get; set; } = string.Empty;

        public QueueItemControl()
        {
            this.InitializeComponent();
        }

        public void SetTitle(string t) => TitleText.Text = t;

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            PauseClicked?.Invoke(this, ItemId);
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            RemoveClicked?.Invoke(this, ItemId);
        }

        public event EventHandler<string>? PauseClicked;
        public event EventHandler<string>? RemoveClicked;
    }
}
