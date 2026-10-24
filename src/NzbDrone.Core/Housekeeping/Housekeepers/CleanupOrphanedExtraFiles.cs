using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedExtraFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedExtraFiles(IMainDatabase database)
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
            mapper.Execute(@"DELETE FROM ""ExtraFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""ExtraFiles"".""Id"" FROM ""ExtraFiles""
                                     LEFT OUTER JOIN ""Platforms""
                                     ON ""ExtraFiles"".""SeriesId"" = ""Platforms"".""Id""
                                     WHERE ""Platforms"".""Id"" IS NULL)");
        }

        private void DeleteOrphanedByEpisodeFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""ExtraFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""ExtraFiles"".""Id"" FROM ""ExtraFiles""
                                     LEFT OUTER JOIN ""RomFiles""
                                     ON ""ExtraFiles"".""EpisodeFileId"" = ""RomFiles"".""Id""
                                     WHERE ""ExtraFiles"".""EpisodeFileId"" > 0
                                     AND ""RomFiles"".""Id"" IS NULL)");
        }

        private void DeleteWhereEpisodeFileIsZero()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""ExtraFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""Id"" FROM ""ExtraFiles""
                                     WHERE ""EpisodeFileId"" = 0)");
        }
    }
}
