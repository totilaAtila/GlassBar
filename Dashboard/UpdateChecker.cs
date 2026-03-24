using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace GlassBar.Dashboard
{
    internal static class UpdateChecker
    {
        private const string ReleasesApiUrl =
            "https://api.github.com/repos/totilaAtila/GlassBar/releases/latest";

        /// <summary>
        /// Checks GitHub for a newer release.
        /// Returns the latest tag name (e.g. "v2.3") if a newer version exists, null otherwise.
        /// Never throws — all errors are swallowed silently.
        /// </summary>
        internal static async Task<string?> CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GlassBar-Dashboard/1.0");

                var release = await client.GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl);
                if (release?.TagName == null) return null;

                var latestTag = release.TagName.TrimStart('v');
                var current   = GetCurrentVersion();

                if (Version.TryParse(latestTag, out var latestVer) &&
                    Version.TryParse(current,    out var currentVer) &&
                    latestVer > currentVer)
                {
                    return release.TagName;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetCurrentVersion()
        {
            // Reads the assembly version (set in project file or AssemblyInfo).
            // Falls back to the hard-coded product version string.
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null)
                return $"{ver.Major}.{ver.Minor}";
            return "2.2";
        }

        private sealed class GitHubRelease
        {
            public string? TagName { get; set; }
            public string? HtmlUrl { get; set; }
        }
    }
}
