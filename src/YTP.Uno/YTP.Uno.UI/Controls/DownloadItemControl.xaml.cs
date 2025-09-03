using Windows.UI.Xaml.Controls;

namespace YTP.Uno.UI.Controls
{
    public sealed partial class DownloadItemControl : UserControl
    {
        public DownloadItemControl()
        {
            this.InitializeComponent();
        }

        public void SetTitle(string t) => TitleText.Text = t;
        public void SetProgress(double p) => ProgressBar.Value = p;
    }
}
