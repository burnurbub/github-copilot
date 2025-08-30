using System;
using System.Text.RegularExpressions;

namespace YTP.Core.Utilities
{
    public static class NameCleaner
    {
        public static string CleanName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim();

            var patterns = new[]
            {
                " - Topic",
                " - Official Audio",
                " - Official Video",
                "(Official Music Video)",
                "(Official Audio)",
                "[Official Music Video]",
                "[Official Audio]"
            };
            foreach (var p in patterns)
            {
                if (s.EndsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - p.Length).Trim();
                }
            }

            if (s.StartsWith("Album - ", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(8).Trim();

            // Remove trailing bracketed channel indicators like " - Topic" or " [channel]" or " (channel)"
            s = Regex.Replace(s, @"\s*[-\(\[]\s*topic\s*[)\]]?\s*$", "", RegexOptions.IgnoreCase).Trim();
            s = Regex.Replace(s, @"\s*[-\(\[]\s*official.*[)\]]?\s*$", "", RegexOptions.IgnoreCase).Trim();

            s = Regex.Replace(s, "\\s+", " ").Trim();
            return s;
        }
    }
}
