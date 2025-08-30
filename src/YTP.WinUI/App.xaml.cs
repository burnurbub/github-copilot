using Microsoft.UI.Xaml;

namespace YTP.WinUI
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var wnd = new MainWindow();
            wnd.Activate();
        }
    }
}
