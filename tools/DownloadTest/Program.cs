using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using YTP.Core.Services;
using YTP.Core.Models;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var url = args.Length > 0 ? args[0] : "https://music.youtube.com/watch?v=SmOyU5LM-F0&si=rZG_yVvsuvMstvlx";
        var ffmpegPath = "ffmpeg"; // assume in PATH
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "download_test_output");
        Directory.CreateDirectory(outputDir);

        var ff = new FFmpegService(ffmpegPath);
        var meta = new MetadataService();
        var downloader = new YoutubeDownloaderService(ff, meta);

        // Obtain basic video info via YoutubeExplode to fill VideoItem
        var client = new YoutubeExplode.YoutubeClient();
        var vid = YoutubeExplode.Videos.VideoId.Parse(url);
        var v = await client.Videos.GetAsync(vid);

        var item = new VideoItem
        {
            Id = v.Id,
            Title = v.Title,
            Channel = v.Author?.ChannelTitle ?? "",
            Description = v.Description,
            ThumbnailUrl = v.Thumbnails?.FirstOrDefault()?.Url?.ToString(),
            UploadDate = v.UploadDate.DateTime,
            Duration = v.Duration
        };

        try
        {
            var path = await downloader.DownloadAudioAsMp3Async(item, outputDir);
            Console.WriteLine($"Downloaded and tagged: {path}");
            var lrc = Path.ChangeExtension(path, ".lrc");
            Console.WriteLine($"LRC sidecar exists: {File.Exists(lrc)} -> {lrc}");
            return 0;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
            return 2;
        }
    }
}
