using System.Threading.Tasks;

namespace YTP.Uno.UI.Platform
{
    public static class ClipboardService
    {
        public static async Task<string?> GetTextAsync()
        {
            try
            {
                var data = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (data != null && data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    return await data.GetTextAsync();
                }
            }
            catch
            {
                // platform may not support clipboard in this host
            }
            return null;
        }
    }
}
