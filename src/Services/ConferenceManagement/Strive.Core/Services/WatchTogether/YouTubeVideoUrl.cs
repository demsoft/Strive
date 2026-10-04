using System;
using System.Text.RegularExpressions;

namespace Strive.Core.Services.WatchTogether
{
    /// <summary>Finds the video in the links to YouTube that people paste.</summary>
    public static class YouTubeVideoUrl
    {
        private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

        private static readonly string[] YouTubeHosts =
            {"youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com"};

        /// <summary>
        ///     Accepts youtube.com/watch?v=ID, youtu.be/ID, youtube.com/live/ID, /shorts/ID and /embed/ID. A start time
        ///     (t=90, t=1m30s, start=90) is returned in seconds.
        /// </summary>
        public static bool TryParse(string? url, out string videoId, out double startSeconds)
        {
            videoId = string.Empty;
            startSeconds = 0;

            if (string.IsNullOrWhiteSpace(url) || url.Length > 2000) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

            var host = uri.Host.ToLowerInvariant();
            string? candidate = null;

            if (host == "youtu.be" || host == "www.youtu.be")
            {
                candidate = FirstSegment(uri.AbsolutePath);
            }
            else if (Array.IndexOf(YouTubeHosts, host) >= 0)
            {
                var path = uri.AbsolutePath.Trim('/');
                if (path.Equals("watch", StringComparison.OrdinalIgnoreCase))
                    candidate = GetQueryValue(uri.Query, "v");
                else if (path.StartsWith("live/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("v/", StringComparison.OrdinalIgnoreCase))
                    candidate = FirstSegment(path[(path.IndexOf('/') + 1)..]);
            }

            if (candidate == null || !VideoIdPattern.IsMatch(candidate)) return false;

            videoId = candidate;
            startSeconds = ParseStart(GetQueryValue(uri.Query, "t") ?? GetQueryValue(uri.Query, "start") ??
                                      GetFragmentStart(uri.Fragment));
            return true;
        }

        private static string FirstSegment(string path)
        {
            var trimmed = path.Trim('/');
            var slash = trimmed.IndexOf('/');
            return slash < 0 ? trimmed : trimmed[..slash];
        }

        private static string? GetQueryValue(string query, string key)
        {
            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                var name = eq < 0 ? part : part[..eq];
                if (name == key) return eq < 0 ? string.Empty : Uri.UnescapeDataString(part[(eq + 1)..]);
            }

            return null;
        }

        private static string? GetFragmentStart(string fragment)
        {
            // youtu.be/ID#t=90
            return fragment.StartsWith("#t=", StringComparison.Ordinal) ? fragment[3..] : null;
        }

        /// <summary>"90", "90s", "1m30s", "1h2m3s"</summary>
        private static double ParseStart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var plain))
                return plain is >= 0 and < 86400 ? Math.Floor(plain) : 0;

            var match = Regex.Match(value, @"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$", RegexOptions.IgnoreCase);
            if (!match.Success) return 0;

            double seconds = 0;
            if (match.Groups[1].Success) seconds += int.Parse(match.Groups[1].Value) * 3600d;
            if (match.Groups[2].Success) seconds += int.Parse(match.Groups[2].Value) * 60d;
            if (match.Groups[3].Success) seconds += int.Parse(match.Groups[3].Value);
            return seconds < 86400 ? seconds : 0;
        }
    }
}
