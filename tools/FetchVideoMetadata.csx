#r "nuget: YTP.Core, 1.0.0"
#r "nuget: YoutubeExplode, 6.1.6"
using System;
using System.Threading.Tasks;
using YTP.Core.Services;

await Task.Run(async () =>
{
    var svc = new YoutubeExplodeService();
    var url = args.Length > 0 ? args[0] : "https://music.youtube.com/watch?v=SmOyU5LM-F0&si=rZG_yVvsuvMstvlx";
    var list = await svc.GetPlaylistOrVideoAsync(url);
    foreach (var v in list)
    {
        Console.WriteLine($"Id: {v.Id}");
        Console.WriteLine($"Title: {v.Title}");
        Console.WriteLine($"Channel: {v.Channel}");
        Console.WriteLine($"Playlist: {v.IsPlaylistItem} / {v.PlaylistTitle} / idx {v.PlaylistIndex} total {v.PlaylistTotal}");
        Console.WriteLine($"Thumbnail: {v.ThumbnailUrl}");
        Console.WriteLine($"Description length: { (v.Description?.Length ?? 0) }");
        Console.WriteLine($"Duration: {v.Duration}");
    }
});
