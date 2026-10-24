using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMetadataFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMetadataFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedBySeries();
            DeleteOrphanedByEpisodeFile();
            DeleteWhereEpisodeFileIsZero();
        }

        private void DeleteOrphanedBySeries()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""Platforms""
                                     ON ""MetadataFiles"".""SeriesId"" = ""Platforms"".""Id""
                                     WHERE ""Platforms"".""Id"" IS NULL)");
        }

        private void DeleteOrphanedByEpisodeFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""RomFiles""
                                     ON ""MetadataFiles"".""EpisodeFileId"" = ""RomFiles"".""Id""
                                     WHERE ""MetadataFiles"".""EpisodeFileId"" > 0
                                     AND ""RomFiles"".""Id"" IS NULL)");
        }

        private void DeleteWhereEpisodeFileIsZero()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""Id"" FROM ""MetadataFiles""
                                     WHERE ""Type"" IN (2, 5)
                                     AND ""EpisodeFileId"" = 0)");
        }
    }
}
