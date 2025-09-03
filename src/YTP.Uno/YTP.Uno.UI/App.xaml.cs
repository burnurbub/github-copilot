using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace YTP.Uno.UI
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var root = Window.Current.Content as Frame;
            if (root == null)
            {
                root = new Frame();
                Window.Current.Content = root;
            }

            if (root.Content == null)
            {
                root.Navigate(typeof(MainPage));
            }

            Window.Current.Activate();
        }
    }
}
