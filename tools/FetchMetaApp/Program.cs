using System;
using System.Threading.Tasks;
using YTP.Core.Services;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var url = args.Length > 0 ? args[0] : "https://music.youtube.com/watch?v=SmOyU5LM-F0&si=rZG_yVvsuvMstvlx";
        var svc = new YoutubeExplodeService();
        try
        {
            var list = await svc.GetPlaylistOrVideoAsync(url);
            foreach (var v in list)
            {
                Console.WriteLine($"Id: {v.Id}");
                Console.WriteLine($"Title: {v.Title}");
                Console.WriteLine($"Channel: {v.Channel}");
                Console.WriteLine($"IsPlaylistItem: {v.IsPlaylistItem}");
                Console.WriteLine($"PlaylistTitle: {v.PlaylistTitle}");
                Console.WriteLine($"PlaylistIndex/Total: {v.PlaylistIndex}/{v.PlaylistTotal}");
                Console.WriteLine($"Thumbnail: {v.ThumbnailUrl}");
                Console.WriteLine($"UploadDate: {v.UploadDate}");
                Console.WriteLine($"Duration: {v.Duration}");
                Console.WriteLine($"Description length: { (v.Description?.Length ?? 0) }");
                Console.WriteLine(new string('-',60));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 2;
        }

        return 0;
    }
}
