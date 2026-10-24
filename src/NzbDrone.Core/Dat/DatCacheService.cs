using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Dat
{
    public class DatCacheService : IDatCacheService
    {
        private readonly IMainDatabase _database;

        public DatCacheService(IMainDatabase database)
        {
            _database = database;
        }

        public DatFile Insert(DatFile datFile, List<DatEntry> datEntries)
        {
            using (var connection = _database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                if (!string.IsNullOrWhiteSpace(datFile.Name))
                {
                    var lowerName = datFile.Name.Trim().ToLowerInvariant();
                    var existingIds = connection.Query<int>(
                        @"SELECT ""Id"" FROM ""DatFiles"" WHERE LOWER(""Name"") = @lowerName",
                        new { lowerName },
                        transaction).ToList();

                    foreach (var existingId in existingIds)
                    {
                        connection.Execute(
                            @"DELETE FROM ""DatEntries"" WHERE ""DatFileId"" = @existingId",
                            new { existingId },
                            transaction);
                        connection.Execute(
                            @"DELETE FROM ""DatFiles"" WHERE ""Id"" = @existingId",
                            new { existingId },
                            transaction);
                    }
                }

                var datFileSql = _database.DatabaseType == DatabaseType.PostgreSQL
                    ? @"INSERT INTO ""DatFiles"" (""Name"", ""Description"", ""Version"", ""Author"", ""Category"", ""CreatedAt"")
                        VALUES (@Name, @Description, @Version, @Author, @Category, @CreatedAt)
                        RETURNING ""Id"";"
                    : @"INSERT INTO ""DatFiles"" (""Name"", ""Description"", ""Version"", ""Author"", ""Category"", ""CreatedAt"")
                        VALUES (@Name, @Description, @Version, @Author, @Category, @CreatedAt);
                        SELECT last_insert_rowid();";

                var datFileId = connection.ExecuteScalar<int>(datFileSql, datFile, transaction);
                datFile.Id = datFileId;

                foreach (var entry in datEntries)
                {
                    entry.DatFileId = datFileId;
                }

                var datEntrySql = @"
                    INSERT INTO ""DatEntries"" (""DatFileId"", ""GameName"", ""RomName"", ""Size"", ""Crc32"", ""Md5"", ""Sha1"", ""Sha256"", ""SmdbPath"", ""Region"", ""Status"")
                    VALUES (@DatFileId, @GameName, @RomName, @Size, @Crc32, @Md5, @Sha1, @Sha256, @SmdbPath, @Region, @Status);";

                connection.Execute(datEntrySql, datEntries, transaction);
                transaction.Commit();
            }

            return datFile;
        }

        public List<DatFile> GetAllDatFiles()
        {
            var sql = @"SELECT * FROM ""DatFiles"" ORDER BY ""CreatedAt"" DESC, ""Id"" DESC";

            using (var connection = _database.OpenConnection())
            {
                return connection.Query<DatFile>(sql).ToList();
            }
        }

        public List<DatEntry> GetEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null)
        {
            var slug = (platformSlug ?? string.Empty).Trim().ToLowerInvariant();
            var name = (platformName ?? slug).Trim().ToLowerInvariant();
            var sys = (libretroSystemName ?? name).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(slug) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(sys))
            {
                return new List<DatEntry>();
            }

            var sql = @"
                SELECT e.*
                FROM ""DatEntries"" e
                INNER JOIN ""DatFiles"" f ON f.""Id"" = e.""DatFileId""
                WHERE LOWER(COALESCE(f.""Description"", '')) = @slug
                   OR LOWER(COALESCE(f.""Name"", '')) = @slug
                   OR LOWER(COALESCE(f.""Name"", '')) LIKE @namePattern
                   OR LOWER(COALESCE(f.""Name"", '')) LIKE @sysPattern
                   OR LOWER(COALESCE(f.""Description"", '')) LIKE @sysPattern";

            using (var connection = _database.OpenConnection())
            {
                return connection.Query<DatEntry>(
                    sql,
                    new
                    {
                        slug,
                        namePattern = $"%{name}%",
                        sysPattern = $"%{sys}%"
                    }).ToList();
            }
        }

        public DatEntry FindByCrc32(string crc32)
        {
            if (string.IsNullOrWhiteSpace(crc32))
            {
                return null;
            }

            var sql = @"SELECT * FROM ""DatEntries"" WHERE ""Crc32"" = @crc32 LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatEntry>(sql, new { crc32 = crc32.ToLowerInvariant() });
            }
        }

        public DatEntry FindBySha1(string sha1)
        {
            if (string.IsNullOrWhiteSpace(sha1))
            {
                return null;
            }

            var sql = @"SELECT * FROM ""DatEntries"" WHERE ""Sha1"" = @sha1 LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatEntry>(sql, new { sha1 = sha1.ToLowerInvariant() });
            }
        }

        public DatEntry FindByMd5(string md5)
        {
            if (string.IsNullOrWhiteSpace(md5))
            {
                return null;
            }

            var sql = @"SELECT * FROM ""DatEntries"" WHERE ""Md5"" = @md5 LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatEntry>(sql, new { md5 = md5.ToLowerInvariant() });
            }
        }

        public DatEntry FindBySha256(string sha256)
        {
            if (string.IsNullOrWhiteSpace(sha256))
            {
                return null;
            }

            var sql = @"SELECT * FROM ""DatEntries"" WHERE ""Sha256"" = @sha256 LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatEntry>(sql, new { sha256 = sha256.ToLowerInvariant() });
            }
        }

        public List<DatEntry> SearchByName(string query, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<DatEntry>();
            }

            var pattern = $"%{query.Trim().ToLowerInvariant()}%";
            var sql = @"SELECT * FROM ""DatEntries"" WHERE LOWER(""GameName"") LIKE @pattern OR LOWER(""RomName"") LIKE @pattern LIMIT @limit";

            using (var connection = _database.OpenConnection())
            {
                return connection.Query<DatEntry>(sql, new { pattern, limit }).ToList();
            }
        }
    }
}
