using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Games
{
    public class SmartAddRequest
    {
        public string Title { get; set; }
        public string PlatformSlug { get; set; }
        public string RootFolderPath { get; set; }
        public int? IgdbId { get; set; }
        public bool AllowUnofficial { get; set; }
        public string ExpectedHash { get; set; }
    }

    public class SmartAddResult
    {
        public bool Accepted { get; set; }
        public string RejectionReason { get; set; }
        public bool PlatformAutoProvisioned { get; set; }
        public GamePlatform Platform { get; set; }
        public GameTitle GameTitle { get; set; }
    }

    public interface ISmartAddService
    {
        List<GameTitle> SearchGlobalGames(string query, bool allowUnofficial = false, string expectedHash = null);
        SmartAddResult SmartAddGame(SmartAddRequest request);
        bool IsUnofficialOrHack(string releaseOrTitleName, ParsedRomInfo parsedRom = null);
        bool VerifyCryptographicHashGate(string hash);
    }

    public class SmartAddService : ISmartAddService
    {
        private static readonly string[] UnofficialKeywords = { "hack", "homebrew", "pirate", "unlicensed", "kaizo", "randomizer" };
        private static readonly string[] UnofficialDumpTags = { "h", "t", "f", "b", "Beta", "Proto", "Demo", "Sample" };

        private readonly IGamePlatformRepository _platformRepository;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly IPlatformCatalogService _platformCatalogService;
        private readonly IDatCacheService _datCacheService;

        public SmartAddService(
            IGamePlatformRepository platformRepository,
            IGameTitleRepository gameTitleRepository,
            IPlatformCatalogService platformCatalogService,
            IDatCacheService datCacheService)
        {
            _platformRepository = platformRepository;
            _gameTitleRepository = gameTitleRepository;
            _platformCatalogService = platformCatalogService;
            _datCacheService = datCacheService;
        }

        public List<GameTitle> SearchGlobalGames(string query, bool allowUnofficial = false, string expectedHash = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<GameTitle>();
            }

            var trimmed = query.Trim();
            var allTitles = _gameTitleRepository.All() ?? Enumerable.Empty<GameTitle>();

            var matches = allTitles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Title) &&
                            (t.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrWhiteSpace(t.CleanTitle) && t.CleanTitle.Contains(trimmed, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            return matches.Where(t =>
            {
                if (!IsUnofficialOrHack(t.Title))
                {
                    return true;
                }

                return allowUnofficial && VerifyCryptographicHashGate(expectedHash);
            }).ToList();
        }

        public SmartAddResult SmartAddGame(SmartAddRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.PlatformSlug))
            {
                return new SmartAddResult
                {
                    Accepted = false,
                    RejectionReason = "Title and PlatformSlug are required."
                };
            }

            var parsedRom = RomParser.ParseRomName(request.Title);
            if (IsUnofficialOrHack(request.Title, parsedRom))
            {
                if (!request.AllowUnofficial || !VerifyCryptographicHashGate(request.ExpectedHash))
                {
                    return new SmartAddResult
                    {
                        Accepted = false,
                        RejectionReason = "Unofficial ROM hacks, patches, and homebrew require AllowUnofficial and a verified cryptographic hash in the DAT/SMDB cache."
                    };
                }
            }

            var existingPlatforms = _platformRepository.All()?.ToList() ?? new List<GamePlatform>();
            var platform = existingPlatforms.FirstOrDefault(p =>
                string.Equals(p.Slug, request.PlatformSlug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, request.PlatformSlug, StringComparison.OrdinalIgnoreCase));

            var autoProvisioned = false;
            if (platform == null)
            {
                var catalogDef = _platformCatalogService?.GetAll()?.FirstOrDefault(c =>
                    string.Equals(c.Slug, request.PlatformSlug, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name, request.PlatformSlug, StringComparison.OrdinalIgnoreCase));

                var platformName = catalogDef?.Name ?? request.PlatformSlug.Trim();
                var slug = catalogDef?.Slug ?? request.PlatformSlug.Trim().ToLowerInvariant();
                var manufacturer = catalogDef?.Manufacturer ?? string.Empty;
                var consoleFolder = catalogDef?.RegionalConsoleNames != null && catalogDef.RegionalConsoleNames.TryGetValue("US", out var usConsole)
                    ? usConsole
                    : platformName;

                var rootFolder = request.RootFolderPath ?? string.Empty;
                var platformPath = !string.IsNullOrWhiteSpace(manufacturer)
                    ? Path.Combine(rootFolder, manufacturer, consoleFolder)
                    : Path.Combine(rootFolder, consoleFolder);

                platform = _platformRepository.Insert(new GamePlatform
                {
                    Name = platformName,
                    Slug = slug,
                    RootFolderPath = rootFolder,
                    Path = platformPath,
                    Monitored = true,
                    CollectionStrategy = CollectionStrategy.OneGameOneRom,
                    RevisionPreference = RevisionPreference.LatestRevision
                });

                autoProvisioned = true;
            }

            var effectiveTitle = !string.IsNullOrWhiteSpace(parsedRom?.GameTitle) ? parsedRom.GameTitle : request.Title.Trim();
            var cleanTitle = Parser.Parser.CleanSeriesTitle(effectiveTitle);

            var existingTitles = _gameTitleRepository.All()?.ToList() ?? new List<GameTitle>();
            var gameTitle = existingTitles.FirstOrDefault(t =>
                t.PlatformId == platform.Id &&
                (string.Equals(t.Title, effectiveTitle, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.CleanTitle, cleanTitle, StringComparison.OrdinalIgnoreCase)));

            if (gameTitle == null)
            {
                gameTitle = _gameTitleRepository.Insert(new GameTitle
                {
                    PlatformId = platform.Id,
                    Title = effectiveTitle,
                    CleanTitle = cleanTitle,
                    IgdbId = request.IgdbId ?? 0,
                    Monitored = true
                });
            }
            else if (!gameTitle.Monitored)
            {
                gameTitle.Monitored = true;
                _gameTitleRepository.Update(gameTitle);
            }

            return new SmartAddResult
            {
                Accepted = true,
                PlatformAutoProvisioned = autoProvisioned,
                Platform = platform,
                GameTitle = gameTitle
            };
        }

        public bool IsUnofficialOrHack(string releaseOrTitleName, ParsedRomInfo parsedRom = null)
        {
            if (string.IsNullOrWhiteSpace(releaseOrTitleName))
            {
                return false;
            }

            var romInfo = parsedRom ?? RomParser.ParseRomName(releaseOrTitleName);
            if (romInfo?.DumpTags != null)
            {
                if (romInfo.DumpTags.Any(t =>
                    UnofficialDumpTags.Contains(t, StringComparer.OrdinalIgnoreCase) ||
                    t.StartsWith("T+", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("T-", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return UnofficialKeywords.Any(k => releaseOrTitleName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        public bool VerifyCryptographicHashGate(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash) || _datCacheService == null)
            {
                return false;
            }

            var normalized = hash.Trim().ToLowerInvariant();
            return _datCacheService.FindBySha256(normalized) != null ||
                   _datCacheService.FindBySha1(normalized) != null ||
                   _datCacheService.FindByMd5(normalized) != null ||
                   _datCacheService.FindByCrc32(normalized) != null;
        }
    }
}
