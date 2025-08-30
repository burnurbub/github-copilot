using System;
using System.Threading;
using System.Threading.Tasks;
using YTP.Core.Download;
using YTP.Core.Services;
using YTP.Core.Settings;

// Tiny console runner to validate core wiring quickly
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("YTP.Core quick runner");
        var settingsManager = new SettingsManager();
        var settings = settingsManager.Settings;

        var yt = new YoutubeExplodeService();
        var ffmpeg = new FFmpegService(settings.FfmpegPath);
        var dm = new DownloadManager(yt, ffmpeg, settings.OutputDirectory);

        dm.LogMessage += s => Console.WriteLine(s);
        dm.ProgressChanged += p => Console.WriteLine($"Progress: {p.Item.Title} - {p.Percentage:P0} - {p.CurrentPhase}");

        Console.WriteLine("No real downloads are performed by the quick runner. Use the library from a UI app.");
        await Task.CompletedTask;
    }
}
