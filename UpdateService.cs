using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BatteryGuardian
{
    /// <summary>
    /// Contains information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>The tag name of the newer release, e.g. "v1.9.0".</summary>
        public string LatestVersion { get; set; } = "";

        /// <summary>The GitHub release page URL.</summary>
        public string ReleaseUrl { get; set; } = "";

        /// <summary>The release notes body (may be empty).</summary>
        public string ReleaseNotes { get; set; } = "";
    }

    /// <summary>
    /// Queries the GitHub Releases API to determine whether a newer version of
    /// Battery Guardian is available. All failures are silent — if the network is
    /// down or GitHub rate-limits us, we simply act as if no update is available.
    /// </summary>
    public class UpdateService
    {
        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/puneet-swarup/Battery-Guardian/releases/latest";

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            // GitHub requires a User-Agent header on all API requests.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BatteryGuardian-UpdateChecker/1.0");
            // Don't wait forever if GitHub is slow.
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        /// <summary>
        /// Checks GitHub for a newer release than <paramref name="currentVersion"/>.
        /// Returns null if up-to-date, or if the check failed for any reason.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion)
        {
            try
            {
                string json = await _http.GetStringAsync(LatestReleaseApiUrl);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.TryGetProperty("tag_name", out var tagEl)
                    ? tagEl.GetString() ?? ""
                    : "";

                string url = root.TryGetProperty("html_url", out var urlEl)
                    ? urlEl.GetString() ?? ""
                    : "";

                string notes = root.TryGetProperty("body", out var bodyEl)
                    ? bodyEl.GetString() ?? ""
                    : "";

                Version? remote = ParseTagToVersion(tag);
                if (remote == null) return null;

                if (remote > currentVersion)
                {
                    return new UpdateInfo
                    {
                        LatestVersion = tag,
                        ReleaseUrl = url,
                        ReleaseNotes = notes
                    };
                }

                return null;
            }
            catch
            {
                // Network failure, rate limit, malformed JSON — treat as "no update".
                return null;
            }
        }

        /// <summary>
        /// Parses a tag like "v1.9.0", "1.9", or "v2" into a Version.
        /// Returns null if the tag cannot be parsed.
        /// Exposed as public static so it can be unit-tested.
        /// </summary>
        public static Version? ParseTagToVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            string cleaned = tag.TrimStart('v', 'V').Trim();
            if (string.IsNullOrEmpty(cleaned)) return null;

            string[] parts = cleaned.Split('.');

            if (!int.TryParse(parts[0], out int major)) return null;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 0;

            return new Version(major, minor, build, 0);
        }
    }
}