using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Analytics;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Update
{
    public interface IUpdatePackageProvider
    {
        UpdatePackage GetLatestUpdate(string branch, Version currentVersion);
        List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null);
    }

    public class UpdatePackageProvider : IUpdatePackageProvider
    {
        private static readonly Regex VersionRegex = new Regex(@"(?<version>\d+\.\d+\.\d+(?:\.\d+)?)", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IHttpRequestBuilderFactory _requestBuilder;
        private readonly IPlatformInfo _platformInfo;
        private readonly IAnalyticsService _analyticsService;
        private readonly IMainDatabase _mainDatabase;
        private readonly Logger _logger;

        public UpdatePackageProvider(IHttpClient httpClient,
                                     ISonarrCloudRequestBuilder requestBuilder,
                                     IAnalyticsService analyticsService,
                                     IPlatformInfo platformInfo,
                                     IMainDatabase mainDatabase,
                                     Logger logger)
        {
            _platformInfo = platformInfo;
            _analyticsService = analyticsService;
            _requestBuilder = requestBuilder?.Services ?? new SonarrCloudRequestBuilder().Services;
            _httpClient = httpClient;
            _mainDatabase = mainDatabase;
            _logger = logger;
        }

        public UpdatePackage GetLatestUpdate(string branch, Version currentVersion)
        {
            var recent = GetRecentUpdates(branch, currentVersion);
            var latest = recent.OrderByDescending(u => u.Version).FirstOrDefault();

            if (latest == null || currentVersion == null || latest.Version <= currentVersion)
            {
                return null;
            }

            return latest;
        }

        public List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null)
        {
            try
            {
                var request = _requestBuilder.Create()
                                             .Resource("releases")
                                             .AddQueryParam("per_page", 20)
                                             .Accept(HttpAccept.Json)
                                             .Build();

                request.Headers.Set("User-Agent", "Controllarr-Updater");

                var response = _httpClient.Get<List<GitHubReleaseResource>>(request);
                var releases = response?.Resource ?? new List<GitHubReleaseResource>();

                var runtimeId = GetCurrentRuntimeIdentifier();
                var packages = new List<UpdatePackage>();

                foreach (var release in releases.Where(r => !r.Draft))
                {
                    var package = MapReleaseToUpdatePackage(release, runtimeId, branch);
                    if (package != null)
                    {
                        packages.Add(package);
                    }
                }

                if (packages.Any())
                {
                    var normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "develop" : branch.ToLowerInvariant();
                    var branchMatches = packages
                        .Where(p => string.Equals(p.Branch, normalizedBranch, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (branchMatches.Any())
                    {
                        return branchMatches.OrderByDescending(p => p.Version).ToList();
                    }
                }

                return packages.OrderByDescending(p => p.Version).ToList();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to retrieve Controllarr update packages from GitHub Releases.");
                return new List<UpdatePackage>();
            }
        }

        private static UpdatePackage MapReleaseToUpdatePackage(GitHubReleaseResource release, string runtimeId, string requestedBranch)
        {
            if (release == null)
            {
                return null;
            }

            var version = ParseVersion(release.TagName) ?? ParseVersion(release.Name);
            if (version == null)
            {
                return null;
            }

            var assets = release.Assets ?? new List<GitHubReleaseAssetResource>();
            var matchingAsset = SelectBestAsset(assets, runtimeId);

            if (matchingAsset == null || string.IsNullOrWhiteSpace(matchingAsset.BrowserDownloadUrl))
            {
                return null;
            }

            var inferredBranch = InferBranchFromAssetName(matchingAsset.Name, release.Prerelease, requestedBranch);
            var sha256Hash = ExtractSha256Digest(matchingAsset.Digest);

            return new UpdatePackage
            {
                Version = version,
                ReleaseDate = release.PublishedAt ?? release.CreatedAt ?? DateTime.UtcNow,
                FileName = matchingAsset.Name,
                Url = matchingAsset.BrowserDownloadUrl,
                Changes = ParseReleaseChanges(release.Body),
                Hash = sha256Hash,
                Branch = inferredBranch
            };
        }

        private static GitHubReleaseAssetResource SelectBestAsset(List<GitHubReleaseAssetResource> assets, string runtimeId)
        {
            if (assets == null || !assets.Any())
            {
                return null;
            }

            var expectedExtension = OsInfo.IsWindows ? ".zip" : ".tar.gz";

            var exactMatch = assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                a.Name.EndsWith($".{runtimeId}{expectedExtension}", StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                return exactMatch;
            }

            var fallbackRuntimeMatch = assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                a.Name.IndexOf(runtimeId, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !a.Name.EndsWith("-installer.exe", StringComparison.OrdinalIgnoreCase) &&
                !a.Name.EndsWith("-app.zip", StringComparison.OrdinalIgnoreCase));

            return fallbackRuntimeMatch;
        }

        private static string GetCurrentRuntimeIdentifier()
        {
            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                _ => "x64"
            };

            if (OsInfo.IsWindows)
            {
                return $"win-{arch}";
            }

            if (OsInfo.IsOsx)
            {
                return $"osx-{arch}";
            }

            if (OsInfo.Os == Os.LinuxMusl)
            {
                return $"linux-musl-{arch}";
            }

            if (OsInfo.Os == Os.Bsd)
            {
                return $"freebsd-{arch}";
            }

            return $"linux-{arch}";
        }

        private static Version ParseVersion(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var match = VersionRegex.Match(input);
            if (!match.Success)
            {
                return null;
            }

            return Version.TryParse(match.Groups["version"].Value, out var parsed) ? parsed : null;
        }

        private static string InferBranchFromAssetName(string assetName, bool isPrerelease, string requestedBranch)
        {
            if (!string.IsNullOrWhiteSpace(assetName))
            {
                var parts = assetName.Split('.');
                if (parts.Length >= 3 &&
                    (parts[0].Equals("Controllarr", StringComparison.OrdinalIgnoreCase) ||
                     parts[0].Equals("Sonarr", StringComparison.OrdinalIgnoreCase)))
                {
                    return parts[1].ToLowerInvariant();
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedBranch))
            {
                return requestedBranch.ToLowerInvariant();
            }

            return isPrerelease ? "develop" : "main";
        }

        private static string ExtractSha256Digest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest))
            {
                return string.Empty;
            }

            const string prefix = "sha256:";
            if (digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return digest.Substring(prefix.Length).Trim();
            }

            return digest.Trim();
        }

        private static UpdateChanges ParseReleaseChanges(string body)
        {
            var changes = new UpdateChanges();

            if (string.IsNullOrWhiteSpace(body))
            {
                return changes;
            }

            var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("<!--", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
                {
                    line = line.Substring(2).Trim();
                }

                if (line.IndexOf("fix", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    changes.Fixed.Add(line);
                }
                else
                {
                    changes.New.Add(line);
                }
            }

            return changes;
        }
    }

    public class GitHubReleaseResource
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetResource> Assets { get; set; }
    }

    public class GitHubReleaseAssetResource
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }
    }
}
