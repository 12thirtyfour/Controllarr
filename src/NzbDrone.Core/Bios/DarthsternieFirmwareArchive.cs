using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Bios
{
    public class DarthsternieFirmwareEntry
    {
        public string GamePlatform { get; set; }
        public string FileName { get; set; }
        public string Version { get; set; }
        public string ExpectedMd5 { get; set; }
        public string Description { get; set; }
        public List<string> DownloadUrls { get; set; } = new List<string>();
    }

    public static class DarthsternieFirmwareArchive
    {
        private static readonly Regex RowRegex = new Regex(
            @"<tr\b[^>]*>(.*?)</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex CellRegex = new Regex(
            @"<td\b[^>]*class\s*=\s*[""'][^""']*column-(\d+)[^""']*[""'][^>]*>(.*?)</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex HrefRegex = new Regex(
            @"href\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex HtmlTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex Md5HexRegex = new Regex(
            @"^[0-9a-f]{32}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly IReadOnlyDictionary<string, string> SourceUrlsByPlatform =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "switch", "https://darthsternie.net/switch-firmwares/" },
                { "ps3", "https://darthsternie.net/ps3-firmwares/" },
                { "psvita", "https://darthsternie.net/ps-vita-firmwares/" },
                { "psp", "https://darthsternie.net/psp-firmwares/" },
                { "3ds", "https://darthsternie.net/3ds-firmwares/" }
            };

        public static List<DarthsternieFirmwareEntry> ParseFirmwareRows(string gamePlatform, string html)
        {
            var results = new List<DarthsternieFirmwareEntry>();
            if (string.IsNullOrWhiteSpace(gamePlatform) || string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match rowMatch in RowRegex.Matches(html))
            {
                var rowHtml = rowMatch.Groups[1].Value;
                if (rowHtml.IndexOf("<th", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                string version = null;
                string md5 = null;
                var urls = new List<string>();

                foreach (Match cellMatch in CellRegex.Matches(rowHtml))
                {
                    var columnIndex = cellMatch.Groups[1].Value;
                    var cellContent = cellMatch.Groups[2].Value;

                    if (columnIndex == "1")
                    {
                        version = StripHtml(cellContent);
                    }
                    else if (columnIndex == "2")
                    {
                        var candidateMd5 = StripHtml(cellContent);
                        if (Md5HexRegex.IsMatch(candidateMd5))
                        {
                            md5 = candidateMd5.ToLowerInvariant();
                        }
                    }
                    else if (columnIndex == "4" || columnIndex == "5")
                    {
                        foreach (Match hrefMatch in HrefRegex.Matches(cellContent))
                        {
                            var href = WebUtility.HtmlDecode(hrefMatch.Groups[1].Value)?.Trim();
                            if (IsSupportedDownloadUrl(href) && !urls.Contains(href, StringComparer.OrdinalIgnoreCase))
                            {
                                urls.Add(href);
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(md5))
                {
                    continue;
                }

                var (fileName, description) = MapPlatformEntryMetadata(gamePlatform, version);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var dedupKey = $"{gamePlatform}|{fileName}|{md5}";
                if (!seenKeys.Add(dedupKey))
                {
                    continue;
                }

                var orderedUrls = urls
                    .OrderBy(u => u.Contains("/folder/", StringComparison.OrdinalIgnoreCase) ||
                                  u.Contains("#F!", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ToList();

                results.Add(new DarthsternieFirmwareEntry
                {
                    GamePlatform = gamePlatform.Trim().ToLowerInvariant(),
                    FileName = fileName,
                    Version = version,
                    ExpectedMd5 = md5,
                    Description = description,
                    DownloadUrls = orderedUrls
                });
            }

            return results;
        }

        private static bool IsSupportedDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            // 1fichier links require interactive browser/CAPTCHA flows and cannot be fetched directly.
            if (uri.Host.Contains("1fichier.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static (string FileName, string Description) MapPlatformEntryMetadata(string gamePlatform, string version)
        {
            var normalizedPlatform = gamePlatform.Trim().ToLowerInvariant();
            switch (normalizedPlatform)
            {
                case "switch":
                    return ($"{version}.zip", $"Nintendo Switch Official {version}");
                case "3ds":
                    return ($"{version}.zip", $"Nintendo 3DS Official Firmware {version}");
                case "ps3":
                    return ("PS3UPDAT.PUP", $"PlayStation 3 Official {version}");
                case "psvita":
                    return ("PSP2UPDAT.PUP", $"PlayStation Vita Official {version}");
                case "psp":
                    return (NormalizePspFileName(version), $"PlayStation Portable Official {version}");
                default:
                    return (null, null);
            }
        }

        private static string NormalizePspFileName(string version)
        {
            var cleaned = version
                .Replace("Firmware", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.All(char.IsDigit))
            {
                return $"{cleaned}.PBP";
            }

            return $"{version}.PBP";
        }

        private static string StripHtml(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var withoutTags = HtmlTagRegex.Replace(raw, string.Empty);
            return WebUtility.HtmlDecode(withoutTags)?.Trim() ?? string.Empty;
        }
    }
}
