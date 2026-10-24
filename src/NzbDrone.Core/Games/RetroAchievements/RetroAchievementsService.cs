using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Games.RetroAchievements
{
    public class RetroAchievementBadge
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Points { get; set; }
        public string BadgeName { get; set; }
        public bool Unlocked { get; set; }
        public bool HardcoreUnlocked { get; set; }
    }

    public class RetroAchievementsGameProgress
    {
        public int GameId { get; set; }
        public string Username { get; set; }
        public string GameTitle { get; set; }
        public string ConsoleName { get; set; }
        public string RomMd5Hash { get; set; }
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public int HardcoreUnlockedAchievements { get; set; }
        public decimal MasteryPercentage { get; set; }
        public bool IsMastered { get; set; }
        public List<RetroAchievementBadge> Badges { get; set; } = new List<RetroAchievementBadge>();
    }

    public class RetroAchievementsSavedCredentials
    {
        public string Username { get; set; }
        public string WebApiKey { get; set; }
    }

    public interface IRetroAchievementsService
    {
        bool ValidateCredentials(string username, string webApiKey);
        RetroAchievementsGameProgress CalculateMasteryProgress(
            string username,
            string gameTitle,
            string romMd5Hash,
            IReadOnlyCollection<RetroAchievementBadge> badges);
        List<RetroAchievementsGameProgress> GetUserLibraryProgress(string username = null, string webApiKey = null);
    }

    public class RetroAchievementsService : IRetroAchievementsService
    {
        private static readonly Regex ApiKeyRegex = new Regex("^[A-Za-z0-9]{16,64}$", RegexOptions.Compiled);
        private static readonly Regex RegionTagRegex = new Regex(@"\s*\([^)]*\)|\s*\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex NonAlphanumericRegex = new Regex(@"[^a-z0-9]+", RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, List<RetroAchievementsGameProgress> Items)> LibraryCache =
            new ConcurrentDictionary<string, (DateTime, List<RetroAchievementsGameProgress>)>(StringComparer.OrdinalIgnoreCase);

        private readonly IHttpClient _httpClient;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly Logger _logger;

        public RetroAchievementsService(
            IHttpClient httpClient = null,
            IAppFolderInfo appFolderInfo = null,
            Logger logger = null)
        {
            _httpClient = httpClient;
            _appFolderInfo = appFolderInfo;
            _logger = logger;
        }

        public bool ValidateCredentials(string username, string webApiKey)
        {
            if (username.IsNullOrWhiteSpace() || webApiKey.IsNullOrWhiteSpace())
            {
                return false;
            }

            var trimmedUser = username.Trim();
            var trimmedKey = webApiKey.Trim();

            var isFormatValid = trimmedUser.Length >= 2 && trimmedUser.Length <= 32 && ApiKeyRegex.IsMatch(trimmedKey);
            if (!isFormatValid)
            {
                return false;
            }

            SaveCredentials(trimmedUser, trimmedKey);
            LibraryCache.TryRemove(trimmedUser, out _);

            return true;
        }

        public List<RetroAchievementsGameProgress> GetUserLibraryProgress(string username = null, string webApiKey = null)
        {
            var resolvedCredentials = ResolveCredentials(username, webApiKey);
            if (resolvedCredentials == null || _httpClient == null)
            {
                return new List<RetroAchievementsGameProgress>();
            }

            var user = resolvedCredentials.Username;
            var key = resolvedCredentials.WebApiKey;

            if (LibraryCache.TryGetValue(user, out var cached) &&
                (DateTime.UtcNow - cached.FetchedAtUtc) < TimeSpan.FromMinutes(5))
            {
                return cached.Items;
            }

            var results = FetchCompletionProgressFromApi(user, key);
            LibraryCache[user] = (DateTime.UtcNow, results);
            return results;
        }

        public RetroAchievementsGameProgress CalculateMasteryProgress(
            string username,
            string gameTitle,
            string romMd5Hash,
            IReadOnlyCollection<RetroAchievementBadge> badges)
        {
            var badgeList = badges != null ? new List<RetroAchievementBadge>(badges) : new List<RetroAchievementBadge>();

            if (badgeList.Count == 0 && _httpClient != null && gameTitle.IsNotNullOrWhiteSpace())
            {
                var remoteProgress = TryFetchRemoteGameProgress(username, gameTitle, romMd5Hash);
                if (remoteProgress != null)
                {
                    return remoteProgress;
                }
            }

            var total = badgeList.Count;
            var unlocked = 0;
            var hardcore = 0;

            foreach (var badge in badgeList)
            {
                if (badge == null)
                {
                    continue;
                }

                if (badge.Unlocked || badge.HardcoreUnlocked)
                {
                    unlocked++;
                }

                if (badge.HardcoreUnlocked)
                {
                    hardcore++;
                }
            }

            var percentage = total > 0
                ? Math.Round((decimal)unlocked / total * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            return new RetroAchievementsGameProgress
            {
                Username = username?.Trim() ?? string.Empty,
                GameTitle = gameTitle?.Trim() ?? string.Empty,
                RomMd5Hash = romMd5Hash?.Trim().ToLowerInvariant() ?? string.Empty,
                TotalAchievements = total,
                UnlockedAchievements = unlocked,
                HardcoreUnlockedAchievements = hardcore,
                MasteryPercentage = percentage,
                IsMastered = total > 0 && hardcore == total,
                Badges = badgeList
            };
        }

        public static string NormalizeGameTitle(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            var withoutRegions = RegionTagRegex.Replace(title, " ").Trim().ToLowerInvariant();
            return NonAlphanumericRegex.Replace(withoutRegions, string.Empty);
        }

        private RetroAchievementsGameProgress TryFetchRemoteGameProgress(string username, string gameTitle, string romMd5Hash)
        {
            try
            {
                var creds = ResolveCredentials(username, null);
                if (creds == null)
                {
                    return null;
                }

                var library = GetUserLibraryProgress(creds.Username, creds.WebApiKey);
                var normalizedTarget = NormalizeGameTitle(gameTitle);
                var matched = library.FirstOrDefault(g => NormalizeGameTitle(g.GameTitle) == normalizedTarget);

                if (matched == null)
                {
                    return null;
                }

                if (matched.GameId <= 0)
                {
                    return matched;
                }

                var url = $"https://retroachievements.org/API/API_GetGameInfoAndUserProgress.php?z={Uri.EscapeDataString(creds.Username)}&y={Uri.EscapeDataString(creds.WebApiKey)}&u={Uri.EscapeDataString(creds.Username)}&g={matched.GameId}";
                var request = new HttpRequest(url)
                {
                    RequestTimeout = TimeSpan.FromSeconds(10),
                    SuppressHttpError = true
                };

                var response = _httpClient.Get(request);
                if (response == null || response.HasHttpError || response.Content.IsNullOrWhiteSpace())
                {
                    return matched;
                }

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;
                var badges = new List<RetroAchievementBadge>();

                if (root.TryGetProperty("Achievements", out var achievementsElem) &&
                    achievementsElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in achievementsElem.EnumerateObject())
                    {
                        var ach = prop.Value;
                        var id = ach.TryGetProperty("ID", out var idEl) && idEl.TryGetInt32(out var parsedId) ? parsedId : 0;
                        var title = ach.TryGetProperty("Title", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;
                        var desc = ach.TryGetProperty("Description", out var dEl) ? dEl.GetString() ?? string.Empty : string.Empty;
                        var pts = ach.TryGetProperty("Points", out var pEl) && pEl.TryGetInt32(out var parsedPts) ? parsedPts : 0;
                        var badgeName = ach.TryGetProperty("BadgeName", out var bEl) ? bEl.GetString() ?? string.Empty : string.Empty;
                        var earnedSoft = ach.TryGetProperty("DateEarned", out var deEl) && deEl.ValueKind == JsonValueKind.String && deEl.GetString().IsNotNullOrWhiteSpace();
                        var earnedHard = ach.TryGetProperty("DateEarnedHardcore", out var dhEl) && dhEl.ValueKind == JsonValueKind.String && dhEl.GetString().IsNotNullOrWhiteSpace();

                        badges.Add(new RetroAchievementBadge
                        {
                            Id = id,
                            Title = title,
                            Description = desc,
                            Points = pts,
                            BadgeName = badgeName,
                            Unlocked = earnedSoft || earnedHard,
                            HardcoreUnlocked = earnedHard
                        });
                    }
                }

                var total = matched.TotalAchievements > 0 ? matched.TotalAchievements : badges.Count;
                var unlocked = matched.UnlockedAchievements > 0 ? matched.UnlockedAchievements : badges.Count(b => b.Unlocked || b.HardcoreUnlocked);
                var hardcore = matched.HardcoreUnlockedAchievements > 0 ? matched.HardcoreUnlockedAchievements : badges.Count(b => b.HardcoreUnlocked);
                var pct = total > 0 ? Math.Round((decimal)unlocked / total * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

                return new RetroAchievementsGameProgress
                {
                    GameId = matched.GameId,
                    Username = creds.Username,
                    GameTitle = gameTitle.Trim(),
                    ConsoleName = matched.ConsoleName,
                    RomMd5Hash = romMd5Hash?.Trim().ToLowerInvariant() ?? string.Empty,
                    TotalAchievements = total,
                    UnlockedAchievements = unlocked,
                    HardcoreUnlockedAchievements = hardcore,
                    MasteryPercentage = pct,
                    IsMastered = total > 0 && hardcore >= total,
                    Badges = badges
                };
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to fetch detailed RetroAchievements game progress for {0}", gameTitle);
                return null;
            }
        }

        private List<RetroAchievementsGameProgress> FetchCompletionProgressFromApi(string username, string webApiKey)
        {
            var results = new List<RetroAchievementsGameProgress>();

            try
            {
                var url = $"https://retroachievements.org/API/API_GetUserCompletionProgress.php?z={Uri.EscapeDataString(username)}&y={Uri.EscapeDataString(webApiKey)}&u={Uri.EscapeDataString(username)}&c=500&o=0";
                var request = new HttpRequest(url)
                {
                    RequestTimeout = TimeSpan.FromSeconds(12),
                    SuppressHttpError = true
                };

                var response = _httpClient.Get(request);
                if (response != null && !response.HasHttpError && response.Content.IsNotNullOrWhiteSpace())
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("Results", out var resultsElem) &&
                        resultsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in resultsElem.EnumerateArray())
                        {
                            var gameId = item.TryGetProperty("GameID", out var gidEl) && gidEl.TryGetInt32(out var gid) ? gid : 0;
                            var title = item.TryGetProperty("Title", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;
                            var consoleName = item.TryGetProperty("ConsoleName", out var cEl) ? cEl.GetString() ?? string.Empty : string.Empty;
                            var maxPossible = item.TryGetProperty("MaxPossible", out var mpEl) && mpEl.TryGetInt32(out var mp) ? mp : 0;
                            var numAwarded = item.TryGetProperty("NumAwarded", out var naEl) && naEl.TryGetInt32(out var na) ? na : 0;
                            var numHardcore = item.TryGetProperty("NumAwardedHardcore", out var nhEl) && nhEl.TryGetInt32(out var nh) ? nh : 0;
                            var highestAward = item.TryGetProperty("HighestAwardKind", out var haEl) ? haEl.GetString() ?? string.Empty : string.Empty;

                            if (title.IsNullOrWhiteSpace() || maxPossible <= 0)
                            {
                                continue;
                            }

                            var unlocked = Math.Max(numAwarded, numHardcore);
                            var isMastered = (maxPossible > 0 && numHardcore >= maxPossible) ||
                                             string.Equals(highestAward, "mastered", StringComparison.OrdinalIgnoreCase);
                            var pct = Math.Round((decimal)unlocked / maxPossible * 100m, 2, MidpointRounding.AwayFromZero);

                            results.Add(new RetroAchievementsGameProgress
                            {
                                GameId = gameId,
                                Username = username,
                                GameTitle = title,
                                ConsoleName = consoleName,
                                TotalAchievements = maxPossible,
                                UnlockedAchievements = unlocked,
                                HardcoreUnlockedAchievements = numHardcore,
                                MasteryPercentage = pct,
                                IsMastered = isMastered
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Failed to query RetroAchievements completion progress for {0}", username);
            }

            return results;
        }

        private RetroAchievementsSavedCredentials ResolveCredentials(string username, string webApiKey)
        {
            if (username.IsNotNullOrWhiteSpace() && webApiKey.IsNotNullOrWhiteSpace())
            {
                var u = username.Trim();
                var k = webApiKey.Trim();
                if (u.Length >= 2 && u.Length <= 32 && ApiKeyRegex.IsMatch(k))
                {
                    SaveCredentials(u, k);
                    return new RetroAchievementsSavedCredentials { Username = u, WebApiKey = k };
                }
            }

            var saved = LoadCredentials();
            if (saved != null &&
                saved.Username.IsNotNullOrWhiteSpace() &&
                saved.WebApiKey.IsNotNullOrWhiteSpace())
            {
                return saved;
            }

            return null;
        }

        private void SaveCredentials(string username, string webApiKey)
        {
            if (_appFolderInfo == null || _appFolderInfo.AppDataFolder.IsNullOrWhiteSpace())
            {
                return;
            }

            try
            {
                var path = Path.Combine(_appFolderInfo.AppDataFolder, "retroachievements.json");
                var json = JsonSerializer.Serialize(new RetroAchievementsSavedCredentials
                {
                    Username = username,
                    WebApiKey = webApiKey
                });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to persist RetroAchievements credentials");
            }
        }

        private RetroAchievementsSavedCredentials LoadCredentials()
        {
            if (_appFolderInfo == null || _appFolderInfo.AppDataFolder.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var path = Path.Combine(_appFolderInfo.AppDataFolder, "retroachievements.json");
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<RetroAchievementsSavedCredentials>(json);
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to read persisted RetroAchievements credentials");
                return null;
            }
        }
    }
}
