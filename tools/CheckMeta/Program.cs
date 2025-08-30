using System;
using YoutubeExplode;
using YoutubeExplode.Videos;
using System.Linq;

var url = args.Length > 0 ? args[0] : "https://music.youtube.com/watch?v=SmOyU5LM-F0&si=rZG_yVvsuvMstvlx";
var client = new YoutubeClient();
try
{
    var id = VideoId.Parse(url);
    var v = await client.Videos.GetAsync(id);
    Console.WriteLine($"Id: {v.Id}");
    Console.WriteLine($"Title: {v.Title}");
    Console.WriteLine($"Author: {v.Author?.ChannelTitle}");
    Console.WriteLine($"UploadDate: {v.UploadDate}");
    Console.WriteLine($"Duration: {v.Duration}");
    Console.WriteLine($"Thumbnails: {string.Join(", ", v.Thumbnails.Select(t=>t.Url))}");
    Console.WriteLine("--- Description ---");
    Console.WriteLine(v.Description?.Substring(0, Math.Min(2000, v.Description?.Length ?? 0)));
}
catch(Exception ex)
{
    Console.WriteLine(ex);
}
