using System.IO;
using YTP.Core.Services;
using Xunit;

namespace YTP.Core.Tests
{
    public class FilenameTests
    {
        [Fact]
        public void SanitizeFilename_RemovesInvalidCharsAndLimitsLength()
        {
            var svc = new YoutubeDownloaderService(null!, null!); // we only use SanitizeFilename via reflection? Instead create a helper local
            var method = typeof(YoutubeDownloaderService).GetMethod("SanitizeFilename", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var longName = new string('a', 300) + "<>:\\|?*";
            var result = (string)method!.Invoke(svc, new object[] { longName })!;
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain("|", result);
            Assert.True(result.Length <= 200);
        }
    }
}
