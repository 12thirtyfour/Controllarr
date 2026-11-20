using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Statistics;

public interface IStatisticsRepository
{
    LibraryStatistics GetLibraryStatistics(StatisticsFilter filter = null);
}

public class StatisticsRepository : IStatisticsRepository
{
    private const string _selectPlatformsTemplate = "SELECT /**select**/ FROM \"Platforms\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/ /**orderby**/";
    private const string _selectGamesTemplate = "SELECT /**select**/ FROM \"Games\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/ /**orderby**/";
    private const string _selectRomFilesTemplate = "SELECT /**select**/ FROM \"RomFiles\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/ /**orderby**/";
    private const string _selectQualityProfilesTemplate = "SELECT /**select**/ FROM \"QualityProfiles\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/ /**orderby**/";
    private const string _selectTagsTemplate = "SELECT /**select**/ FROM \"Tags\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/ /**orderby**/";

    private readonly IMainDatabase _database;

    public StatisticsRepository(IMainDatabase database)
    {
        _database = database;
    }

    public LibraryStatistics GetLibraryStatistics(StatisticsFilter filter = null)
    {
        var currentDate = DateTime.UtcNow;
        var platformFilter = BuildPlatformFilter(filter);

        var platformCounts = QuerySingle<PlatformCounts>(PlatformsBuilder(platformFilter), _selectPlatformsTemplate);
        var completedPlatformCounts = QueryGrouped<CompletedPlatformCounts>(
            CompletedPlatformsBuilder(currentDate, platformFilter),
            _selectGamesTemplate,
            @"COALESCE(SUM(CASE WHEN ""EpisodeCount"" > 0 AND ""EpisodeCount"" = ""EpisodeFileCount"" THEN 1 ELSE 0 END), 0) AS CompletedPlatformCount");
        var regionCounts = QueryGrouped<RegionCounts>(
            RegionsBuilder(currentDate, platformFilter),
            _selectGamesTemplate,
            @"COUNT(*) AS RegionCount, COALESCE(SUM(CASE WHEN ""EpisodeCount"" > 0 AND ""EpisodeCount"" = ""EpisodeFileCount"" THEN 1 ELSE 0 END), 0) AS CompletedRegionCount");
        var gameCounts = QuerySingle<GameCounts>(GamesBuilder(currentDate, platformFilter), _selectGamesTemplate);
        var romFileCounts = QuerySingle<RomFileCounts>(RomFilesBuilder(platformFilter), _selectRomFilesTemplate);
        var qualityProfileCounts = Query<QualityProfileCounts>(QualityProfilesBuilder(platformFilter), _selectQualityProfilesTemplate);
        var qualityProfileFileCounts = Query<QualityProfileFileCounts>(RomFilesPerProfileBuilder(platformFilter), _selectRomFilesTemplate);
        var qualityCounts = Query<QualityCounts>(RomFilesPerQualityBuilder(platformFilter), _selectRomFilesTemplate);
        var tagCounts = Query<TagCounts>(TagsBuilder(platformFilter), _selectTagsTemplate);
        var tagFileCounts = Query<TagFileCounts>(RomFilesPerTagBuilder(platformFilter), _selectRomFilesTemplate);

        return new LibraryStatistics
        {
            PlatformCount = platformCounts.PlatformCount,
            MonitoredPlatformCount = platformCounts.MonitoredPlatformCount,
            CompletedPlatformCount = completedPlatformCounts.CompletedPlatformCount,
            ContinuingPlatformCount = platformCounts.ContinuingPlatformCount,
            EndedPlatformCount = platformCounts.EndedPlatformCount,
            UpcomingPlatformCount = platformCounts.UpcomingPlatformCount,
            DeletedPlatformCount = platformCounts.DeletedPlatformCount,
            StandardPlatformCount = platformCounts.StandardPlatformCount,
            DailyPlatformCount = platformCounts.DailyPlatformCount,
            AnimePlatformCount = platformCounts.AnimePlatformCount,
            RegionCount = regionCounts.RegionCount,
            CompletedRegionCount = regionCounts.CompletedRegionCount,
            TotalGameCount = gameCounts.TotalGameCount,
            MonitoredGameCount = gameCounts.MonitoredGameCount,
            DownloadedGameCount = gameCounts.DownloadedGameCount,
            MissingGameCount = gameCounts.MissingGameCount,
            UnreleasedGameCount = gameCounts.UnreleasedGameCount,
            RomFileCount = romFileCounts.RomFileCount,
            SizeOnDisk = romFileCounts.SizeOnDisk,
            QualityProfileStatistics = MapQualityProfileStatistics(qualityProfileCounts, qualityProfileFileCounts),
            QualityStatistics = MapQualityStatistics(qualityCounts),
            TagStatistics = MapTagStatistics(tagCounts, tagFileCounts)
        };
    }

    private PlatformFilter BuildPlatformFilter(StatisticsFilter filter)
    {
        if (filter == null)
        {
            return null;
        }

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.RootFolderPaths?.Count > 0)
        {
            var pathConditions = new List<string>();

            for (var i = 0; i < filter.RootFolderPaths.Count; i++)
            {
                // Ensure a trailing separator so '/games' doesn't match platforms under '/games2'
                var path = filter.RootFolderPaths[i];
                var separator = path.Contains('\\') ? '\\' : '/';
                var pathPrefix = path.TrimEnd('/', '\\') + separator;

                // SUBSTR instead of LIKE so characters in the path aren't treated as wildcards
                pathConditions.Add($@"SUBSTR(""Platforms"".""Path"", 1, @pathPrefixLength{i}) = @pathPrefix{i}");
                parameters.Add($"pathPrefix{i}", pathPrefix, null);
                parameters.Add($"pathPrefixLength{i}", pathPrefix.Length, null);
            }

            conditions.Add(BuildConditionGroup(pathConditions, filter.RootFolderPathsNot));
        }

        if (filter.TagIds?.Count > 0)
        {
            var tagConditions = new List<string>();

            for (var i = 0; i < filter.TagIds.Count; i++)
            {
                tagConditions.Add(PlatformHasTagExpression($"@tagIdFilter{i}"));
                parameters.Add($"tagIdFilter{i}", filter.TagIds[i], null);
            }

            conditions.Add(BuildConditionGroup(tagConditions, filter.TagIdsNot));
        }

        if (filter.QualityProfileIds?.Count > 0)
        {
            var profileConditions = new List<string>();

            for (var i = 0; i < filter.QualityProfileIds.Count; i++)
            {
                profileConditions.Add($@"""Platforms"".""QualityProfileId"" = @qualityProfileIdFilter{i}");
                parameters.Add($"qualityProfileIdFilter{i}", filter.QualityProfileIds[i], null);
            }

            conditions.Add(BuildConditionGroup(profileConditions, filter.QualityProfileIdsNot));
        }

        if (filter.Monitored.HasValue)
        {
            conditions.Add(@"""Platforms"".""Monitored"" = @monitoredFilter");
            parameters.Add("monitoredFilter", filter.Monitored.Value, null);
        }

        if (filter.SeriesTypes?.Count > 0)
        {
            var seriesTypeConditions = new List<string>();

            for (var i = 0; i < filter.SeriesTypes.Count; i++)
            {
                seriesTypeConditions.Add($@"""Platforms"".""SeriesType"" = @seriesTypeFilter{i}");
                parameters.Add($"seriesTypeFilter{i}", (int)filter.SeriesTypes[i], null);
            }

            conditions.Add(BuildConditionGroup(seriesTypeConditions, filter.SeriesTypesNot));
        }

        if (conditions.Count == 0)
        {
            return null;
        }

        return new PlatformFilter
        {
            Condition = string.Join(" AND ", conditions),
            Parameters = parameters
        };
    }

    private static string BuildConditionGroup(List<string> conditions, bool negate)
    {
        var group = $"({string.Join(" OR ", conditions)})";

        return negate ? $"NOT {group}" : group;
    }

    private static List<QualityProfileStatistics> MapQualityProfileStatistics(List<QualityProfileCounts> profileCounts, List<QualityProfileFileCounts> fileCounts)
    {
        var fileCountsByProfile = fileCounts.ToDictionary(f => f.QualityProfileId);

        return profileCounts.Select(p =>
        {
            var files = fileCountsByProfile.GetValueOrDefault(p.QualityProfileId);

            return new QualityProfileStatistics
            {
                QualityProfileId = p.QualityProfileId,
                Name = p.Name,
                PlatformCount = p.PlatformCount,
                RomFileCount = files?.RomFileCount ?? 0,
                SizeOnDisk = files?.SizeOnDisk ?? 0
            };
        }).ToList();
    }

    private static List<TagStatistics> MapTagStatistics(List<TagCounts> tagCounts, List<TagFileCounts> fileCounts)
    {
        var fileCountsByTag = fileCounts.ToDictionary(f => f.TagId);

        return tagCounts.Select(t =>
        {
            var files = fileCountsByTag.GetValueOrDefault(t.TagId);

            return new TagStatistics
            {
                TagId = t.TagId,
                Label = t.Label,
                PlatformCount = t.PlatformCount,
                RomFileCount = files?.RomFileCount ?? 0,
                SizeOnDisk = files?.SizeOnDisk ?? 0
            };
        }).ToList();
    }

    private static List<QualityStatistics> MapQualityStatistics(List<QualityCounts> qualityCounts)
    {
        return qualityCounts.OrderBy(q => q.QualityId)
                            .Select(q => new QualityStatistics
                            {
                                Quality = Quality.FindById(q.QualityId),
                                RomFileCount = q.RomFileCount,
                                SizeOnDisk = q.SizeOnDisk
                            })
                            .ToList();
    }

    private T QuerySingle<T>(SqlBuilder builder, string template)
    {
        return Query<T>(builder, template).Single();
    }

    private List<T> Query<T>(SqlBuilder builder, string template)
    {
        var sql = builder.AddTemplate(template).LogQuery();

        using var conn = _database.OpenConnection();

        return conn.Query<T>(sql.RawSql, sql.Parameters).ToList();
    }

    private T QueryGrouped<T>(SqlBuilder builder, string template, string outerSelect)
    {
        var sql = builder.AddTemplate(template).LogQuery();
        var wrappedSql = $"SELECT {outerSelect} FROM ({sql.RawSql}) AS \"Grouped\"";

        using var conn = _database.OpenConnection();

        return conn.Query<T>(wrappedSql, sql.Parameters).Single();
    }

    // Per-group sums matching the platform statistics GameCount semantics
    private SqlBuilder GameProgressBuilder(DateTime currentDate)
    {
        var parameters = new DynamicParameters();
        parameters.Add("currentDate", currentDate, null);

        var trueIndicator = _database.DatabaseType == DatabaseType.PostgreSQL ? "true" : "1";

        return new SqlBuilder(_database.DatabaseType)
            .Select($@"SUM(CASE WHEN (""Games"".""Monitored"" = {trueIndicator} AND ""Games"".""AirDateUtc"" <= @currentDate) OR ""Games"".""EpisodeFileId"" > 0 THEN 1 ELSE 0 END) AS ""EpisodeCount"",
                         SUM(CASE WHEN ""Games"".""EpisodeFileId"" > 0 THEN 1 ELSE 0 END) AS ""EpisodeFileCount""",
                parameters)
            .Where(@"""Games"".""SeasonNumber"" != 99");
    }

    private SqlBuilder CompletedPlatformsBuilder(DateTime currentDate, PlatformFilter platformFilter)
    {
        var builder = GameProgressBuilder(currentDate)
            .GroupBy(@"""Games"".""SeriesId""");

        if (platformFilter != null)
        {
            builder.Join<Episode, Series>((e, s) => e.SeriesId == s.Id)
                   .Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder RegionsBuilder(DateTime currentDate, PlatformFilter platformFilter)
    {
        var builder = GameProgressBuilder(currentDate)
            .Where(@"""Games"".""SeasonNumber"" > 0")
            .GroupBy(@"""Games"".""SeriesId"", ""Games"".""SeasonNumber""");

        if (platformFilter != null)
        {
            builder.Join<Episode, Series>((e, s) => e.SeriesId == s.Id)
                   .Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder PlatformsBuilder(PlatformFilter platformFilter)
    {
        var parameters = new DynamicParameters();
        parameters.Add("continuing", (int)SeriesStatusType.Continuing, null);
        parameters.Add("ended", (int)SeriesStatusType.Ended, null);
        parameters.Add("upcoming", (int)SeriesStatusType.Upcoming, null);
        parameters.Add("deleted", (int)SeriesStatusType.Deleted, null);
        parameters.Add("standard", (int)SeriesTypes.Standard, null);
        parameters.Add("daily", (int)SeriesTypes.Daily, null);
        parameters.Add("anime", (int)SeriesTypes.Anime, null);

        var trueIndicator = _database.DatabaseType == DatabaseType.PostgreSQL ? "true" : "1";

        var builder = new SqlBuilder(_database.DatabaseType)
            .Select($@"COUNT(*) AS PlatformCount,
                         COALESCE(SUM(CASE WHEN ""Monitored"" = {trueIndicator} THEN 1 ELSE 0 END), 0) AS MonitoredPlatformCount,
                         COALESCE(SUM(CASE WHEN ""Status"" = @continuing THEN 1 ELSE 0 END), 0) AS ContinuingPlatformCount,
                         COALESCE(SUM(CASE WHEN ""Status"" = @ended THEN 1 ELSE 0 END), 0) AS EndedPlatformCount,
                         COALESCE(SUM(CASE WHEN ""Status"" = @upcoming THEN 1 ELSE 0 END), 0) AS UpcomingPlatformCount,
                         COALESCE(SUM(CASE WHEN ""Status"" = @deleted THEN 1 ELSE 0 END), 0) AS DeletedPlatformCount,
                         COALESCE(SUM(CASE WHEN ""SeriesType"" = @standard THEN 1 ELSE 0 END), 0) AS StandardPlatformCount,
                         COALESCE(SUM(CASE WHEN ""SeriesType"" = @daily THEN 1 ELSE 0 END), 0) AS DailyPlatformCount,
                         COALESCE(SUM(CASE WHEN ""SeriesType"" = @anime THEN 1 ELSE 0 END), 0) AS AnimePlatformCount",
                parameters);

        if (platformFilter != null)
        {
            builder.Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder GamesBuilder(DateTime currentDate, PlatformFilter platformFilter)
    {
        var parameters = new DynamicParameters();
        parameters.Add("currentDate", currentDate, null);

        var trueIndicator = _database.DatabaseType == DatabaseType.PostgreSQL ? "true" : "1";

        var builder = new SqlBuilder(_database.DatabaseType)
            .Select($@"COUNT(*) AS TotalGameCount,
                         COALESCE(SUM(CASE WHEN ""Games"".""Monitored"" = {trueIndicator} THEN 1 ELSE 0 END), 0) AS MonitoredGameCount,
                         COALESCE(SUM(CASE WHEN ""Games"".""EpisodeFileId"" > 0 THEN 1 ELSE 0 END), 0) AS DownloadedGameCount,
                         COALESCE(SUM(CASE WHEN ""Games"".""EpisodeFileId"" = 0 AND ""Games"".""Monitored"" = {trueIndicator} AND ""Platforms"".""Monitored"" = {trueIndicator} AND ""Games"".""AirDateUtc"" <= @currentDate THEN 1 ELSE 0 END), 0) AS MissingGameCount,
                         COALESCE(SUM(CASE WHEN ""Games"".""EpisodeFileId"" = 0 AND (""Games"".""AirDateUtc"" IS NULL OR ""Games"".""AirDateUtc"" > @currentDate) THEN 1 ELSE 0 END), 0) AS UnreleasedGameCount",
                parameters)
            .Join<Episode, Series>((e, s) => e.SeriesId == s.Id)
            .Where(@"""Games"".""SeasonNumber"" != 99");

        if (platformFilter != null)
        {
            builder.Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder RomFilesBuilder(PlatformFilter platformFilter)
    {
        var builder = new SqlBuilder(_database.DatabaseType)
            .Select(@"COUNT(*) AS RomFileCount,
                        COALESCE(SUM(COALESCE(""RomFiles"".""Size"", 0)), 0) AS SizeOnDisk")
            .Where(@"""RomFiles"".""SeasonNumber"" != 99");

        if (platformFilter != null)
        {
            builder.Join<EpisodeFile, Series>((f, s) => f.SeriesId == s.Id)
                   .Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder QualityProfilesBuilder(PlatformFilter platformFilter)
    {
        // Filtering in the join keeps profiles without matching platforms in the results
        var seriesJoin = @"""Platforms"" ON ""Platforms"".""QualityProfileId"" = ""QualityProfiles"".""Id""";

        if (platformFilter != null)
        {
            seriesJoin += $" AND {platformFilter.Condition}";
        }

        return new SqlBuilder(_database.DatabaseType)
            .Select(@"""QualityProfiles"".""Id"" AS QualityProfileId,
                        ""QualityProfiles"".""Name"" AS Name,
                        COUNT(""Platforms"".""Id"") AS PlatformCount")
            .LeftJoin(seriesJoin, platformFilter?.Parameters)
            .GroupBy(@"""QualityProfiles"".""Id"", ""QualityProfiles"".""Name""")
            .OrderBy(@"""QualityProfiles"".""Name""");
    }

    private SqlBuilder RomFilesPerProfileBuilder(PlatformFilter platformFilter)
    {
        var builder = new SqlBuilder(_database.DatabaseType)
            .Select(@"""Platforms"".""QualityProfileId"" AS QualityProfileId,
                        COUNT(*) AS RomFileCount,
                        COALESCE(SUM(COALESCE(""RomFiles"".""Size"", 0)), 0) AS SizeOnDisk")
            .Join<EpisodeFile, Series>((f, s) => f.SeriesId == s.Id)
            .GroupBy(@"""Platforms"".""QualityProfileId""");

        if (platformFilter != null)
        {
            builder.Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private string PlatformHasTagExpression(string tagId)
    {
        return _database.DatabaseType == DatabaseType.PostgreSQL
            ? $@"COALESCE(""Platforms"".""Tags"", '[]')::jsonb @> jsonb_build_array({tagId})"
            : $@"EXISTS (SELECT 1 FROM json_each(COALESCE(""Platforms"".""Tags"", '[]')) AS ""seriesTag"" WHERE ""seriesTag"".""value"" = {tagId})";
    }

    private SqlBuilder TagsBuilder(PlatformFilter platformFilter)
    {
        // Filtering in the join keeps tags without matching platforms in the results
        var seriesJoin = $@"""Platforms"" ON {PlatformHasTagExpression(@"""Tags"".""Id""")}";

        if (platformFilter != null)
        {
            seriesJoin += $" AND {platformFilter.Condition}";
        }

        return new SqlBuilder(_database.DatabaseType)
            .Select(@"""Tags"".""Id"" AS TagId,
                        ""Tags"".""Label"" AS Label,
                        COUNT(""Platforms"".""Id"") AS PlatformCount")
            .LeftJoin(seriesJoin, platformFilter?.Parameters)
            .GroupBy(@"""Tags"".""Id"", ""Tags"".""Label""")
            .OrderBy(@"""Tags"".""Label""");
    }

    private SqlBuilder RomFilesPerTagBuilder(PlatformFilter platformFilter)
    {
        var builder = new SqlBuilder(_database.DatabaseType)
            .Select(@"""Tags"".""Id"" AS TagId,
                        COUNT(*) AS RomFileCount,
                        COALESCE(SUM(COALESCE(""RomFiles"".""Size"", 0)), 0) AS SizeOnDisk")
            .Join<EpisodeFile, Series>((f, s) => f.SeriesId == s.Id)
            .Join($@"""Tags"" ON {PlatformHasTagExpression(@"""Tags"".""Id""")}")
            .GroupBy(@"""Tags"".""Id""");

        if (platformFilter != null)
        {
            builder.Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private SqlBuilder RomFilesPerQualityBuilder(PlatformFilter platformFilter)
    {
        var qualityExpression = _database.DatabaseType == DatabaseType.PostgreSQL
            ? @"(""RomFiles"".""Quality""::json->>'quality')::int"
            : @"CAST(JSON_EXTRACT(""RomFiles"".""Quality"", '$.quality') AS INTEGER)";

        var builder = new SqlBuilder(_database.DatabaseType)
            .Select($@"{qualityExpression} AS QualityId,
                        COUNT(*) AS RomFileCount,
                        COALESCE(SUM(COALESCE(""RomFiles"".""Size"", 0)), 0) AS SizeOnDisk")
            .GroupBy(qualityExpression);

        if (platformFilter != null)
        {
            builder.Join<EpisodeFile, Series>((f, s) => f.SeriesId == s.Id)
                   .Where(platformFilter.Condition, platformFilter.Parameters);
        }

        return builder;
    }

    private class PlatformFilter
    {
        public string Condition { get; set; }
        public DynamicParameters Parameters { get; set; }
    }

    private class PlatformCounts
    {
        public int PlatformCount { get; set; }
        public int MonitoredPlatformCount { get; set; }
        public int ContinuingPlatformCount { get; set; }
        public int EndedPlatformCount { get; set; }
        public int UpcomingPlatformCount { get; set; }
        public int DeletedPlatformCount { get; set; }
        public int StandardPlatformCount { get; set; }
        public int DailyPlatformCount { get; set; }
        public int AnimePlatformCount { get; set; }
    }

    private class CompletedPlatformCounts
    {
        public int CompletedPlatformCount { get; set; }
    }

    private class RegionCounts
    {
        public int RegionCount { get; set; }
        public int CompletedRegionCount { get; set; }
    }

    private class GameCounts
    {
        public int TotalGameCount { get; set; }
        public int MonitoredGameCount { get; set; }
        public int DownloadedGameCount { get; set; }
        public int MissingGameCount { get; set; }
        public int UnreleasedGameCount { get; set; }
    }

    private class RomFileCounts
    {
        public int RomFileCount { get; set; }
        public long SizeOnDisk { get; set; }
    }

    private class QualityProfileCounts
    {
        public int QualityProfileId { get; set; }
        public string Name { get; set; }
        public int PlatformCount { get; set; }
    }

    private class QualityProfileFileCounts
    {
        public int QualityProfileId { get; set; }
        public int RomFileCount { get; set; }
        public long SizeOnDisk { get; set; }
    }

    private class QualityCounts
    {
        public int QualityId { get; set; }
        public int RomFileCount { get; set; }
        public long SizeOnDisk { get; set; }
    }

    private class TagCounts
    {
        public int TagId { get; set; }
        public string Label { get; set; }
        public int PlatformCount { get; set; }
    }

    private class TagFileCounts
    {
        public int TagId { get; set; }
        public int RomFileCount { get; set; }
        public long SizeOnDisk { get; set; }
    }
}
