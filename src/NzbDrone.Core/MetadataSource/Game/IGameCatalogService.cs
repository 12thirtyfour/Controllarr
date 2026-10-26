using System;
using System.Collections.Generic;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Game
{
    public class PlatformCatalogEntry
    {
        public int TvdbId { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Slug { get; set; }
        public string Manufacturer { get; set; }
        public int ReleaseYear { get; set; }
        public string LibretroSystemName { get; set; }
        public string RepresentativeBoxartName { get; set; }
        public string Overview { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        public List<string> Extensions { get; set; } = new List<string>();
    }

    public class GameCatalogEntry
    {
        public int TvdbId { get; set; }
        public string Title { get; set; }
        public string BaseTitle { get; set; }
        public string RevisionTag { get; set; }
        public bool IsLatestRevision { get; set; }
        public bool IsInitialRelease { get; set; }
        public string NoIntroName { get; set; }
        public int PlatformTvdbId { get; set; }
        public string PlatformSlug { get; set; }
        public string PlatformName { get; set; }
        public string PlatformShortName { get; set; }
        public string Manufacturer { get; set; }
        public int RegionSeasonNumber { get; set; }
        public string RegionName { get; set; }
        public int EpisodeNumber { get; set; }
        public int ReleaseYear { get; set; }
        public string ReleaseDateString { get; set; }
        public DateTime FirstAired { get; set; }
        public string Overview { get; set; }
        public string PosterUrl { get; set; }
        public string VerificationSource { get; set; }
        public string ExpectedCrc32 { get; set; }
        public string ExpectedMd5 { get; set; }
        public string ExpectedSha1 { get; set; }
        public string ExpectedSha256 { get; set; }
    }

    public interface IGameCatalogService
    {
        bool IsGameTvdbId(int tvdbId);
        bool TryGetGameEntry(int tvdbId, out GameCatalogEntry entry);
        bool TryGetPlatformEntry(int tvdbId, out PlatformCatalogEntry entry);
        bool TryGetPlatformEntryBySlug(string slug, out PlatformCatalogEntry entry);
        void RegisterGameSelection(int gameTvdbId);
        bool HasPendingGameSelections(int platformTvdbId);
        HashSet<int> GetSelectedGameIdsForPlatform(int platformTvdbId);
        void ClearPendingPlatformSelections(int platformTvdbId);
        void InvalidatePlatformCache(int? platformTvdbId = null);
        int SyncPlatformDatCatalogs(int platformTvdbId, bool forceRefresh = false, string seasonType = null);
        PlatformCatalogEntry InferPlatformForText(string text);
        Episode CreateEpisodeForGame(GameCatalogEntry game, int seriesId, bool monitored);
    }
}
