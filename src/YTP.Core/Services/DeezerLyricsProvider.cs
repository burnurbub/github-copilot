using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YTP.Core.Services
{
    // Minimal Deezer lyrics helper based on lucida's approach: search via public API then call gw-light.php pageTrack
    public class DeezerLyricsProvider
    {
        private readonly HttpClient _http;
        private const string DeezerSearchUrl = "https://api.deezer.com/search";
        private const string GwLight = "https://www.deezer.com/ajax/gw-light.php";

        public DeezerLyricsProvider(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<string?> FetchLyricsAsync(string artist, string title, CancellationToken ct = default)
        {
            try
            {
                var id = await FindTrackIdAsync(artist, title, ct).ConfigureAwait(false);
                if (id == null) return null;

                var lyrics = await GetLyricsForTrackIdAsync(id.Value, ct).ConfigureAwait(false);
                if (lyrics == null) return null;

                // If there are synced lines, join them into a plain text fallback separated by newlines.
                if (lyrics.SyncedLines?.Any() == true)
                    return string.Join('\n', lyrics.SyncedLines.Select(l => l.Line).Where(s => !string.IsNullOrWhiteSpace(s)));

                if (!string.IsNullOrWhiteSpace(lyrics.PlainText)) return lyrics.PlainText;
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<(int timeInMs, string text)>?> FetchSyncedLyricsAsync(string artist, string title, CancellationToken ct = default)
        {
            try
            {
                var id = await FindTrackIdAsync(artist, title, ct).ConfigureAwait(false);
                if (id == null) return null;

                var lyrics = await GetLyricsForTrackIdAsync(id.Value, ct).ConfigureAwait(false);
                if (lyrics?.SyncedLines == null || lyrics.SyncedLines.Length == 0) return null;

                var list = new List<(int, string)>();
                foreach (var l in lyrics.SyncedLines)
                {
                    if (int.TryParse(l.Milliseconds, out var ms))
                    {
                        var text = l.Line ?? string.Empty;
                        list.Add((ms, text.Trim() == "♪" ? string.Empty : text.Trim()));
                    }
                }
                return list;
            }
            catch
            {
                return null;
            }
        }

        private async Task<int?> FindTrackIdAsync(string artist, string title, CancellationToken ct)
        {
            // Prefer simple deezer search with artist and track query
            try
            {
                var q = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(artist)) q.Append($"artist:\"{EscapeDeezerQuery(artist)}\" ");
                if (!string.IsNullOrWhiteSpace(title)) q.Append($"track:\"{EscapeDeezerQuery(title)}\"");

                var url = $"{DeezerSearchUrl}?q={System.Web.HttpUtility.UrlEncode(q.ToString())}&limit=3";
                using var res = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return null;
                using var s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in data.EnumerateArray())
                    {
                        try
                        {
                            if (it.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                                return idProp.GetInt32();
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string EscapeDeezerQuery(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // remove quotes
            return s.Replace('"', ' ');
        }

        private async Task<DeezerLyricsResult?> GetLyricsForTrackIdAsync(int id, CancellationToken ct)
        {
            try
            {
                // Build gw-light url with random cid like lucida
                var cid = new Random().Next(100000, 99999999).ToString();
                var url = $"{GwLight}?method=deezer.pageTrack&input=3&api_version=1.0&api_token=&cid={cid}";

                var payload = new { sng_id = id, lang = "en" };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "text/plain");

                // headers mirroring lucida can help
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") { CharSet = "UTF-8" };
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Origin", "https://www.deezer.com");
                request.Headers.Add("Referer", "https://www.deezer.com/");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");

                using var res = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return null;
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    // LYRICS may be present under results.LYRICS
                    if (results.TryGetProperty("LYRICS", out var lyricsElem))
                    {
                        var plain = lyricsElem.TryGetProperty("LYRICS_TEXT", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

                        DeezerSyncedLine[] synced = Array.Empty<DeezerSyncedLine>();
                        if (lyricsElem.TryGetProperty("LYRICS_SYNC_JSON", out var sync) && sync.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<DeezerSyncedLine>();
                            foreach (var it in sync.EnumerateArray())
                            {
                                try
                                {
                                    var line = it.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
                                    var milli = it.TryGetProperty("milliseconds", out var ms) && ms.ValueKind == JsonValueKind.String ? ms.GetString() : (it.TryGetProperty("milliseconds", out var msm) && msm.ValueKind == JsonValueKind.Number ? msm.GetRawText() : null);
                                    var dur = it.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : (it.TryGetProperty("duration", out var dm) && dm.ValueKind == JsonValueKind.Number ? dm.GetRawText() : null);
                                    list.Add(new DeezerSyncedLine { Line = line, Milliseconds = milli, Duration = dur });
                                }
                                catch { }
                            }
                            synced = list.ToArray();
                        }

                        return new DeezerLyricsResult { PlainText = plain, SyncedLines = synced };
                    }
                }
            }
            catch { }
            return null;
        }

        private class DeezerLyricsResult
        {
            public string? PlainText { get; set; }
            public DeezerSyncedLine[]? SyncedLines { get; set; }
        }

        private class DeezerSyncedLine
        {
            public string? Milliseconds { get; set; }
            public string? Duration { get; set; }
            public string? Line { get; set; }
        }
    }
}
