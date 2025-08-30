using System;
using TagLib;
using Xunit;

namespace YTP.Core.Tests
{
    public class MetadataPrintTest
    {
        [Fact]
        public void PrintMp3MetadataFromEnv()
        {
            var path = Environment.GetEnvironmentVariable("YTP_TEST_MP3");
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("YTP_TEST_MP3 is not set. Skipping metadata print test.");
                return;
            }

            var file = TagLib.File.Create(path);
            Console.WriteLine($"File: {path}");
            Console.WriteLine($"Title: {file.Tag.Title}");
            Console.WriteLine($"Artists: {string.Join(", ", file.Tag.Performers ?? new string[0])}");
            Console.WriteLine($"Album: {file.Tag.Album}");
            Console.WriteLine($"Year: {file.Tag.Year}");
            Console.WriteLine($"Track: {file.Tag.Track}/{file.Tag.TrackCount}");
            Console.WriteLine($"Lyrics: {(string.IsNullOrWhiteSpace(file.Tag.Lyrics) ? "<none>" : file.Tag.Lyrics.Substring(0, Math.Min(200, file.Tag.Lyrics.Length)))}");

            if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
            {
                Console.WriteLine($"Has {file.Tag.Pictures.Length} picture(s). First mime: {file.Tag.Pictures[0].MimeType}");
            }
            else
            {
                Console.WriteLine("No pictures embedded.");
            }
        }
    }
}
