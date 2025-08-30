using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace YTP.Core.Services
{
    public class FFmpegService
    {
        private readonly string _ffmpegPath;

        public FFmpegService(string ffmpegPath)
        {
            _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
        }

        public Task ConvertToMp3Async(string inputFile, string outputFile, string quality = "320k", CancellationToken ct = default)
        {
            // Basic ffmpeg invocation: ffmpeg -y -i input -vn -ab 320k -ar 44100 -f mp3 output
            var args = $"-y -i \"{inputFile}\" -vn -ab {quality} -ar 44100 -f mp3 \"{outputFile}\"";
            return RunProcessAsync(_ffmpegPath, args, ct);
        }

        private async Task RunProcessAsync(string fileName, string arguments, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<object?>();
            using var proc = new Process();
            proc.StartInfo.FileName = fileName;
            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e) => tcs.TrySetResult(null);

            proc.Start();

            using (ct.Register(() => {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
                tcs.TrySetCanceled();
            }))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
