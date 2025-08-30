using System;
using System.Threading.Tasks;
using YTP.Core.Services;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        var id = args.Length > 0 ? args[0] : "-W20dfeNCmI";
        var svc = new MetadataService();
        Console.WriteLine($"Testing synced lyrics for video id: {id}");
        var lines = await svc.FetchSyncedLyricsAsync(id, default);
        if (lines == null) Console.WriteLine("No synced lyrics returned from combined providers.");
        else
        {
            foreach (var l in lines)
                Console.WriteLine($"{l.timeInMs} -> {l.text}");
        }

        // Also try plain lyrics extraction via title/artist -> ScrapeLyricsAsync
        try
        {
            var youtube = new YoutubeExplode.YoutubeClient();
            var vid = await youtube.Videos.GetAsync(id);
            var artist = vid.Author?.ChannelTitle ?? string.Empty;
            var title = vid.Title ?? string.Empty;
            Console.WriteLine($"\nTesting plain lyrics scrape for: {artist} - {title}");
            var plain = await svc.ScrapeLyricsAsync(title, artist, vid.Description, default);
            if (string.IsNullOrWhiteSpace(plain)) Console.WriteLine("No plain lyrics returned.");
            else Console.WriteLine(plain.Substring(0, Math.Min(1000, plain.Length)) + "\n---(truncated)---");

            // Try Deezer provider directly
            var dzp = new DeezerLyricsProvider(new System.Net.Http.HttpClient());
            var dz = await dzp.FetchSyncedLyricsAsync(artist, title, default);
            Console.WriteLine("\nDeezer synced lines:");
            if (dz == null) Console.WriteLine("No deezer synced lyrics");
            else foreach (var l in dz) Console.WriteLine($"{l.timeInMs} -> {l.text}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during extra checks: " + ex.Message);
        }
        return 0;
    }
}
