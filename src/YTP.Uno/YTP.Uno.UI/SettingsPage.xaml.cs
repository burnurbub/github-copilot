using System;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using YTP.Core.Settings;

namespace YTP.Uno.UI
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsManager _sm = new SettingsManager();

        public SettingsPage()
        {
            this.InitializeComponent();
            OutputFolderText.Text = _sm.Settings.OutputFolder ?? string.Empty;
            FfmpegPathText.Text = _sm.Settings.FfmpegPath ?? string.Empty;
        }

        private async void ChooseOutput_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null) OutputFolderText.Text = folder.Path;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _sm.Update(s => {
                s.OutputFolder = OutputFolderText.Text;
                s.FfmpegPath = FfmpegPathText.Text;
            });
            if (this.Frame != null) this.Frame.GoBack();
            else if (NavigationService != null) NavigationService.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null) this.Frame.GoBack();
            else if (NavigationService != null) NavigationService.GoBack();
        }
    }
}
