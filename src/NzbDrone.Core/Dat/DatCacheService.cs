using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Dat.Events;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Games;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Dat
{
    public class DatCacheService : IDatCacheService
    {
        private readonly IMainDatabase _database;
        private readonly IEventAggregator _eventAggregator;

        public DatCacheService(IMainDatabase database, IEventAggregator eventAggregator = null)
        {
            _database = database;
            _eventAggregator = eventAggregator;
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

            _eventAggregator?.PublishEvent(new DatDatabaseUpdatedEvent(datFile, datEntries?.Count ?? 0));

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

        private static readonly HashSet<string> KnownPlatformSlugs = PlatformCatalogService.KnownPlatformSlugs;

        public List<DatEntry> GetEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null)
        {
            using (var connection = _database.OpenConnection())
            {
                var matchedFileIds = GetMatchedDatFileIds(connection, platformSlug, platformName, libretroSystemName);
                if (matchedFileIds.Count == 0)
                {
                    return new List<DatEntry>();
                }

                var idList = string.Join(",", matchedFileIds);
                var sql = $@"
                    SELECT *
                    FROM ""DatEntries""
                    WHERE ""DatFileId"" IN ({idList})";

                return connection.Query<DatEntry>(sql).ToList();
            }
        }

        public bool HasEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null)
        {
            using (var connection = _database.OpenConnection())
            {
                var matchedFileIds = GetMatchedDatFileIds(connection, platformSlug, platformName, libretroSystemName);
                if (matchedFileIds.Count == 0)
                {
                    return false;
                }

                var idList = string.Join(",", matchedFileIds);
                var sql = $@"
                    SELECT 1
                    FROM ""DatEntries""
                    WHERE ""DatFileId"" IN ({idList})
                    LIMIT 1";

                return connection.ExecuteScalar<int?>(sql).HasValue;
            }
        }

        private static List<int> GetMatchedDatFileIds(System.Data.IDbConnection connection, string platformSlug, string platformName, string libretroSystemName)
        {
            var slug = (platformSlug ?? string.Empty).Trim().ToLowerInvariant();
            var name = (platformName ?? slug).Trim().ToLowerInvariant();
            var sys = (libretroSystemName ?? name).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(slug) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(sys))
            {
                return new List<int>();
            }

            var datFiles = connection.Query<DatFile>(@"SELECT ""Id"", ""Name"", ""Description"" FROM ""DatFiles""").ToList();
            var matchedFileIds = new List<int>();

            foreach (var file in datFiles)
            {
                if (IsDatFilePlatformMatch(file, slug, name, sys))
                {
                    matchedFileIds.Add(file.Id);
                }
            }

            return matchedFileIds;
        }

        private static bool IsDatFilePlatformMatch(DatFile file, string slug, string name, string sys)
        {
            var desc = (file.Description ?? string.Empty).Trim().ToLowerInvariant();
            var fileName = (file.Name ?? string.Empty).Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(desc) && KnownPlatformSlugs.Contains(desc))
            {
                return desc == slug;
            }

            if (HasPlatformSubstringCollision(slug, fileName))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(slug) && (desc == slug || fileName == slug))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(name) && (fileName.Contains(name) || desc.Contains(name)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(sys) && (fileName.Contains(sys) || desc.Contains(sys)))
            {
                return true;
            }

            return false;
        }

        private static bool HasPlatformSubstringCollision(string targetSlug, string datFileName)
        {
            if (string.IsNullOrWhiteSpace(datFileName))
            {
                return false;
            }

            switch (targetSlug)
            {
                case "nes":
                    return datFileName.Contains("super nintendo") || datFileName.Contains("snes") || datFileName.Contains("famicom disk system") || datFileName.Contains("fds");
                case "gb":
                    return datFileName.Contains("game boy color") || datFileName.Contains("gbc") || datFileName.Contains("game boy advance") || datFileName.Contains("gba");
                case "gbc":
                    return datFileName.Contains("game boy advance") || datFileName.Contains("gba");
                case "ps1":
                    return datFileName.Contains("playstation 2") || datFileName.Contains("ps2") ||
                           datFileName.Contains("playstation 3") || datFileName.Contains("ps3") ||
                           datFileName.Contains("psp") || datFileName.Contains("vita");
                case "ps2":
                    return datFileName.Contains("playstation 3") || datFileName.Contains("ps3");
                case "xbox":
                    return datFileName.Contains("360") || datFileName.Contains("one");
                case "wii":
                    return datFileName.Contains("wii u") || datFileName.Contains("wiiu") || datFileName.Contains("wiiware") || datFileName.Contains("(digital)");
                case "wiiu":
                    return datFileName.Contains("eshop") || datFileName.Contains("(digital)");
                case "sms":
                    return datFileName.Contains("genesis") || datFileName.Contains("mega drive") || datFileName.Contains("sega cd") || datFileName.Contains("game gear");
                case "tg16":
                    return datFileName.Contains("tgcd") || datFileName.Contains("turbografx-cd") || datFileName.Contains("pc engine cd");
                case "neogeo":
                    return datFileName.Contains("pocket") || datFileName.Contains("ngp");
                case "nds":
                    return datFileName.Contains("3ds") || datFileName.Contains("dsiware") || datFileName.Contains("dsi shop") || datFileName.Contains("(digital)");
                case "3ds":
                    return datFileName.Contains("eshop") || datFileName.Contains("3dsware") || datFileName.Contains("(digital)");
                case "switch":
                    return datFileName.Contains("eshop") || datFileName.Contains("(digital)");
                default:
                    return false;
            }
        }

        public DatEntry FindByCrc32(string crc32)
        {
            if (string.IsNullOrWhiteSpace(crc32) || string.Equals(crc32.Trim(), "00000000", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var sql = @"SELECT * FROM ""DatEntries"" WHERE ""Crc32"" = @crc32 LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatEntry>(sql, new { crc32 = crc32.Trim().ToLowerInvariant() });
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

        public DatFile GetDatFileById(int id)
        {
            var sql = @"SELECT * FROM ""DatFiles"" WHERE ""Id"" = @id LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatFile>(sql, new { id });
            }
        }

        public DatEntry FindBestMatch(string crc32, string md5 = null, string sha1 = null, string sha256 = null, string platformSlug = null, string platformName = null)
        {
            using (var connection = _database.OpenConnection())
            {
                return FindBestMatchWithConnection(connection, crc32, md5, sha1, sha256, platformSlug, platformName);
            }
        }

        public DatEntryWithPlatform FindPlatformByHash(string crc32, string sha1 = null, string md5 = null, string sha256 = null)
        {
            using (var connection = _database.OpenConnection())
            {
                var entry = FindBestMatchWithConnection(connection, crc32, md5, sha1, sha256, null, null);
                if (entry == null)
                {
                    return null;
                }

                var datFile = connection.QueryFirstOrDefault<DatFile>(@"SELECT * FROM ""DatFiles"" WHERE ""Id"" = @id LIMIT 1", new { id = entry.DatFileId });
                var desc = datFile?.Description?.Trim().ToLowerInvariant();
                var platformSlug = !string.IsNullOrWhiteSpace(desc) && KnownPlatformSlugs.Contains(desc)
                    ? desc
                    : PlatformCompatibilityValidator.NormalizePlatformSlug(null, datFile?.Name ?? datFile?.Description);

                return new DatEntryWithPlatform
                {
                    DatEntry = entry,
                    DatFile = datFile,
                    PlatformSlug = platformSlug
                };
            }
        }

        private static DatEntry FindBestMatchWithConnection(
            System.Data.IDbConnection connection,
            string crc32,
            string md5,
            string sha1,
            string sha256,
            string platformSlug,
            string platformName)
        {
            var normSha256 = string.IsNullOrWhiteSpace(sha256) ? null : sha256.Trim().ToLowerInvariant();
            var normSha1 = string.IsNullOrWhiteSpace(sha1) ? null : sha1.Trim().ToLowerInvariant();
            var normMd5 = string.IsNullOrWhiteSpace(md5) ? null : md5.Trim().ToLowerInvariant();
            var normCrc32 = string.IsNullOrWhiteSpace(crc32) || string.Equals(crc32.Trim(), "00000000", System.StringComparison.OrdinalIgnoreCase)
                ? null
                : crc32.Trim().ToLowerInvariant();

            if (normSha256 == null && normSha1 == null && normMd5 == null && normCrc32 == null)
            {
                return null;
            }

            var conditions = new List<string>(4);
            var parameters = new DynamicParameters();

            if (normSha256 != null)
            {
                conditions.Add(@"""Sha256"" = @normSha256");
                parameters.Add("normSha256", normSha256);
            }

            if (normSha1 != null)
            {
                conditions.Add(@"""Sha1"" = @normSha1");
                parameters.Add("normSha1", normSha1);
            }

            if (normMd5 != null)
            {
                conditions.Add(@"""Md5"" = @normMd5");
                parameters.Add("normMd5", normMd5);
            }

            if (normCrc32 != null)
            {
                conditions.Add(@"""Crc32"" = @normCrc32");
                parameters.Add("normCrc32", normCrc32);
            }

            var sql = $@"SELECT * FROM ""DatEntries"" WHERE {string.Join(" OR ", conditions)} LIMIT 25";
            var candidates = connection.Query<DatEntry>(sql, parameters).ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            HashSet<int> matchedFileIds = null;
            if (!string.IsNullOrWhiteSpace(platformSlug) || !string.IsNullOrWhiteSpace(platformName))
            {
                var ids = GetMatchedDatFileIds(connection, platformSlug, platformName, null);
                if (ids.Count > 0)
                {
                    matchedFileIds = new HashSet<int>(ids);
                }
            }

            return candidates
                .OrderByDescending(c => matchedFileIds != null && matchedFileIds.Contains(c.DatFileId) ? 1 : 0)
                .ThenByDescending(c => ScoreHashStrength(c, normSha256, normSha1, normMd5, normCrc32))
                .First();
        }

        private static int ScoreHashStrength(DatEntry candidate, string sha256, string sha1, string md5, string crc32)
        {
            if (sha256 != null && string.Equals(candidate.Sha256, sha256, System.StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (sha1 != null && string.Equals(candidate.Sha1, sha1, System.StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (md5 != null && string.Equals(candidate.Md5, md5, System.StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (crc32 != null && string.Equals(candidate.Crc32, crc32, System.StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
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
