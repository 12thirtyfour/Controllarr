using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.History
{
    public interface IHistoryRepository : IBasicRepository<GameHistory>
    {
        GameHistory MostRecentForEpisode(int episodeId);
        List<GameHistory> FindByEpisodeId(int episodeId);
        GameHistory MostRecentForDownloadId(string downloadId);
        List<GameHistory> FindByDownloadId(string downloadId);
        List<GameHistory> GetBySeries(int seriesId, GameHistoryEventType? eventType);
        List<GameHistory> GetBySeason(int seriesId, int seasonNumber, GameHistoryEventType? eventType);
        List<GameHistory> GetByEpisode(int episodeId, GameHistoryEventType? eventType);
        List<GameHistory> FindDownloadHistory(int idSeriesId, QualityModel quality);
        void DeleteForSeries(List<int> seriesIds);
        List<GameHistory> Since(DateTime date, GameHistoryEventType? eventType);
        PagingSpec<GameHistory> GetPaged(PagingSpec<GameHistory> pagingSpec, int[] languages, int[] qualities);
    }

    public class HistoryRepository : BasicRepository<GameHistory>, IHistoryRepository
    {
        public HistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public GameHistory MostRecentForEpisode(int episodeId)
        {
            return Query(h => h.EpisodeId == episodeId).MaxBy(h => h.Date);
        }

        public List<GameHistory> FindByEpisodeId(int episodeId)
        {
            return Query(h => h.EpisodeId == episodeId)
                        .OrderByDescending(h => h.Date)
                        .ToList();
        }

        public GameHistory MostRecentForDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId).MaxBy(h => h.Date);
        }

        public List<GameHistory> FindByDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId);
        }

        public List<GameHistory> GetBySeries(int seriesId, GameHistoryEventType? eventType)
        {
            var builder = Builder().Join<GameHistory, Series>((h, a) => h.SeriesId == a.Id)
                                   .Join<GameHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                                   .Where<GameHistory>(h => h.SeriesId == seriesId);

            if (eventType.HasValue)
            {
                builder.Where<GameHistory>(h => h.EventType == eventType);
            }

            return Query(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<GameHistory> GetBySeason(int seriesId, int seasonNumber, GameHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<GameHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Join<GameHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Where<GameHistory>(h => h.SeriesId == seriesId && h.Episode.SeasonNumber == seasonNumber);

            if (eventType.HasValue)
            {
                builder.Where<GameHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<GameHistory, Episode>(
                builder,
                (history, episode) =>
                {
                    history.Episode = episode;
                    return history;
                }).OrderByDescending(h => h.Date).ToList();
        }

        public List<GameHistory> GetByEpisode(int episodeId, GameHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<GameHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<GameHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Where<GameHistory>(h => h.EpisodeId == episodeId);

            if (eventType.HasValue)
            {
                builder.Where<GameHistory>(h => h.EventType == eventType);
            }

            return Query(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<GameHistory> FindDownloadHistory(int idSeriesId, QualityModel quality)
        {
            return Query(h =>
                 h.SeriesId == idSeriesId &&
                 h.Quality == quality &&
                 (h.EventType == GameHistoryEventType.Grabbed ||
                 h.EventType == GameHistoryEventType.DownloadFailed ||
                 h.EventType == GameHistoryEventType.DownloadFolderImported))
                 .ToList();
        }

        public void DeleteForSeries(List<int> seriesIds)
        {
            Delete(c => seriesIds.Contains(c.SeriesId));
        }

        public List<GameHistory> Since(DateTime date, GameHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<GameHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<GameHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Where<GameHistory>(x => x.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<GameHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<GameHistory, Series, Episode>(builder, (history, series, episode) =>
            {
                history.Series = series;
                history.Episode = episode;
                return history;
            }).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<GameHistory> GetPaged(PagingSpec<GameHistory> pagingSpec, int[] languages, int[] qualities)
        {
            var sortingByQuality = string.Equals(pagingSpec.SortKey, "quality", StringComparison.OrdinalIgnoreCase);
            var customSortExpression = sortingByQuality ? "COALESCE(\"r\".\"Score\", -1)" : null;

            pagingSpec.Records = GetPagedRecords(PagedBuilder(languages, qualities, sortingByQuality), pagingSpec, PagedQuery, customSortExpression);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(languages, qualities, sortingByQuality).Select(typeof(GameHistory)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        private SqlBuilder PagedBuilder(int[] languages, int[] qualities, bool joinQualityRanks)
        {
            var builder = Builder()
                .Join<GameHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<GameHistory, Episode>((h, a) => h.EpisodeId == a.Id);

            if (joinQualityRanks)
            {
                var qualityIdExpr = _database.DatabaseType == DatabaseType.PostgreSQL
                    ? "(\"History\".\"Quality\"::jsonb ->> 'quality')::int"
                    : "json_extract(\"History\".\"Quality\", '$.quality')";

                builder.LeftJoin(
                    $"\"QualityProfileQualityRanks\" AS \"r\" " +
                    $"ON \"r\".\"ProfileId\" = \"Platforms\".\"QualityProfileId\" " +
                    $"AND \"r\".\"QualityId\" = {qualityIdExpr}");
            }

            if (languages is { Length: > 0 })
            {
                builder.Where($"({BuildLanguageWhereClause(languages)})");
            }

            if (qualities is { Length: > 0 })
            {
                builder.Where($"({BuildQualityWhereClause(qualities)})");
            }

            return builder;
        }

        protected override IEnumerable<GameHistory> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<GameHistory, Series, Episode>(builder, (history, series, episode) =>
            {
                history.Series = series;
                history.Episode = episode;
                return history;
            });

        private string BuildLanguageWhereClause(int[] languages)
        {
            var clauses = new List<string>();

            foreach (var language in languages)
            {
                // There are 4 different types of values we should see:
                // - Not the last value in the array
                // - When it's the last value in the array and on different OSes
                // - When it was converted from a single language

                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\".\"Languages\" LIKE '[% {language},%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(13) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(10) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\".\"Languages\" LIKE '[{language}]'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }

        private string BuildQualityWhereClause(int[] qualities)
        {
            var clauses = new List<string>();

            foreach (var quality in qualities)
            {
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(GameHistory))}\".\"Quality\" LIKE '%_quality_: {quality},%'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }
    }
}
