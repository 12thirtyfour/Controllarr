using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedEpisodeFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedEpisodeFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""RomFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""RomFiles"".""Id"" FROM ""RomFiles""
                                     LEFT OUTER JOIN ""Games""
                                     ON ""RomFiles"".""Id"" = ""Games"".""EpisodeFileId""
                                     WHERE ""Games"".""Id"" IS NULL)");
        }
    }
}
