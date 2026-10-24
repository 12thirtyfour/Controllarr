using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedEpisodes : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedEpisodes(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Games""
                                     WHERE ""Id"" IN (
                                     SELECT ""Games"".""Id"" FROM ""Games""
                                     LEFT OUTER JOIN ""Platforms""
                                     ON ""Games"".""SeriesId"" = ""Platforms"".""Id""
                                     WHERE ""Platforms"".""Id"" IS NULL)");
        }
    }
}
