using System;
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

                var siblingDatFileIds = GetSiblingPlatformDatFileIds(connection, transaction, datFile);
                var hashIndex = new Dictionary<string, DatEntry>(StringComparer.OrdinalIgnoreCase);
                var entriesToUpdate = new HashSet<DatEntry>();
                var duplicateIdsToDelete = new List<int>();

                if (siblingDatFileIds.Count > 0)
                {
                    var siblingIdList = string.Join(",", siblingDatFileIds);
                    var existingSiblingEntries = connection.Query<DatEntry>(
                        $@"SELECT * FROM ""DatEntries"" WHERE ""DatFileId"" IN ({siblingIdList}) ORDER BY ""Id""",
                        transaction: transaction).ToList();

                    foreach (var siblingEntry in existingSiblingEntries)
                    {
                        DatEntryHashHelper.NormalizeEntryHashes(siblingEntry);
                        if (DatEntryHashHelper.HasAnyHash(siblingEntry) &&
                            DatEntryHashHelper.TryFindMatchingEntry(hashIndex, siblingEntry, out var canonicalSibling))
                        {
                            if (DatEntryHashHelper.MergeDatEntry(canonicalSibling, siblingEntry, hashIndex))
                            {
                                entriesToUpdate.Add(canonicalSibling);
                            }

                            duplicateIdsToDelete.Add(siblingEntry.Id);
                            continue;
                        }

                        DatEntryHashHelper.IndexEntryHashes(hashIndex, siblingEntry);
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

                var entriesToInsert = new List<DatEntry>();
                if (datEntries != null)
                {
                    foreach (var entry in datEntries)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        DatEntryHashHelper.NormalizeEntryHashes(entry);
                        entry.DatFileId = datFileId;

                        if (DatEntryHashHelper.HasAnyHash(entry) &&
                            DatEntryHashHelper.TryFindMatchingEntry(hashIndex, entry, out var existing))
                        {
                            if (DatEntryHashHelper.MergeDatEntry(existing, entry, hashIndex) && existing.Id > 0)
                            {
                                entriesToUpdate.Add(existing);
                            }

                            continue;
                        }

                        entriesToInsert.Add(entry);
                        DatEntryHashHelper.IndexEntryHashes(hashIndex, entry);
                    }
                }

                if (duplicateIdsToDelete.Count > 0)
                {
                    foreach (var batch in duplicateIdsToDelete.Chunk(500))
                    {
                        var batchIdList = string.Join(",", batch);
                        connection.Execute(
                            $@"DELETE FROM ""DatEntries"" WHERE ""Id"" IN ({batchIdList})",
                            transaction: transaction);
                    }
                }

                if (entriesToUpdate.Count > 0)
                {
                    var updateSql = @"
                        UPDATE ""DatEntries""
                        SET ""GameName"" = @GameName,
                            ""RomName"" = @RomName,
                            ""Size"" = @Size,
                            ""Crc32"" = @Crc32,
                            ""Md5"" = @Md5,
                            ""Sha1"" = @Sha1,
                            ""Sha256"" = @Sha256,
                            ""SmdbPath"" = @SmdbPath,
                            ""Region"" = @Region,
                            ""Status"" = @Status
                        WHERE ""Id"" = @Id;";

                    connection.Execute(updateSql, entriesToUpdate.ToList(), transaction);
                }

                if (entriesToInsert.Count > 0)
                {
                    var datEntrySql = @"
                        INSERT INTO ""DatEntries"" (""DatFileId"", ""GameName"", ""RomName"", ""Size"", ""Crc32"", ""Md5"", ""Sha1"", ""Sha256"", ""SmdbPath"", ""Region"", ""Status"")
                        VALUES (@DatFileId, @GameName, @RomName, @Size, @Crc32, @Md5, @Sha1, @Sha256, @SmdbPath, @Region, @Status);";

                    connection.Execute(datEntrySql, entriesToInsert, transaction);
                }

                var syncedEntryCount = entriesToInsert.Count + entriesToUpdate.Count;
                transaction.Commit();
                _eventAggregator?.PublishEvent(new DatDatabaseUpdatedEvent(datFile, syncedEntryCount));
            }

            return datFile;
        }

        private static List<int> GetSiblingPlatformDatFileIds(
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            DatFile datFile)
        {
            var desc = (datFile?.Description ?? string.Empty).Trim();
            var name = (datFile?.Name ?? string.Empty).Trim();
            var slug = !string.IsNullOrWhiteSpace(desc) && KnownPlatformSlugs.Contains(desc)
                ? desc.ToLowerInvariant()
                : PlatformCompatibilityValidator.NormalizePlatformSlug(null, string.IsNullOrWhiteSpace(desc) ? name : $"{desc} {name}");

            var allFiles = connection.Query<DatFile>(
                @"SELECT ""Id"", ""Name"", ""Description"" FROM ""DatFiles""",
                transaction: transaction).ToList();

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return allFiles
                    .Where(f => IsDatFilePlatformMatch(f, slug, slug, slug))
                    .Select(f => f.Id)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(desc))
            {
                return allFiles
                    .Where(f => string.Equals((f.Description ?? string.Empty).Trim(), desc, StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.Id)
                    .ToList();
            }

            return new List<int>();
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
                case "snes":
                    return datFileName.Contains("sufami turbo") || datFileName.Contains("satellaview");
                case "gb":
                    return datFileName.Contains("game boy color") || datFileName.Contains("gbc") || datFileName.Contains("game boy advance") || datFileName.Contains("gba");
                case "gbc":
                    return datFileName.Contains("game boy advance") || datFileName.Contains("gba");
                case "gba":
                    return datFileName.Contains("e-reader") || datFileName.Contains("ereader");
                case "ps1":
                    return datFileName.Contains("playstation 2") || datFileName.Contains("ps2") ||
                           datFileName.Contains("playstation 3") || datFileName.Contains("ps3") ||
                           datFileName.Contains("psp") || datFileName.Contains("vita");
                case "ps2":
                    return datFileName.Contains("playstation 3") || datFileName.Contains("ps3");
                case "ps3":
                    return datFileName.Contains("(psn)") || datFileName.Contains("ps3 psn") || datFileName.Contains("playstation 3 psn");
                case "psp":
                    return datFileName.Contains("(psn)") || datFileName.Contains("psp psn") || datFileName.Contains("playstation portable psn");
                case "xbox":
                    return datFileName.Contains("360") || datFileName.Contains("one");
                case "xbox360":
                    return datFileName.Contains("(digital)") || datFileName.Contains("games on demand") || datFileName.Contains("title updates") || datFileName.Contains("xbla");
                case "wii":
                    return datFileName.Contains("wii u") || datFileName.Contains("wiiu") || datFileName.Contains("wiiware") || datFileName.Contains("(digital)");
                case "wiiu":
                    return datFileName.Contains("eshop") || datFileName.Contains("(digital)");
                case "sms":
                    return datFileName.Contains("genesis") || datFileName.Contains("mega drive") || datFileName.Contains("sega cd") || datFileName.Contains("game gear");
                case "pico":
                    return datFileName.Contains("beena");
                case "tg16":
                    return datFileName.Contains("tgcd") || datFileName.Contains("turbografx-cd") || datFileName.Contains("pc engine cd") || datFileName.Contains("supergrafx");
                case "neogeo":
                    return datFileName.Contains("pocket") || datFileName.Contains("ngp") || datFileName.Contains("neo geo cd") || datFileName.Contains("neo-geo cd") || datFileName.Contains("neogeocd");
                case "ngp":
                    return datFileName.Contains("color") || datFileName.Contains("ngpc");
                case "ws":
                    return datFileName.Contains("color") || datFileName.Contains("wsc");
                case "odyssey2":
                    return datFileName.Contains("videopac+");
                case "cassettevision":
                    return datFileName.Contains("super cassette vision");
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

            var hashQueries = new List<(string Sql, string Hash)>(4);

            if (normSha256 != null)
            {
                hashQueries.Add((@"SELECT * FROM ""DatEntries"" WHERE ""Sha256"" = @hash LIMIT 100", normSha256));
            }

            if (normSha1 != null)
            {
                hashQueries.Add((@"SELECT * FROM ""DatEntries"" WHERE ""Sha1"" = @hash LIMIT 100", normSha1));
            }

            if (normMd5 != null)
            {
                hashQueries.Add((@"SELECT * FROM ""DatEntries"" WHERE ""Md5"" = @hash LIMIT 100", normMd5));
            }

            if (normCrc32 != null)
            {
                hashQueries.Add((@"SELECT * FROM ""DatEntries"" WHERE ""Crc32"" = @hash LIMIT 100", normCrc32));
            }

            List<DatEntry> candidates = null;

            foreach (var (sql, hash) in hashQueries)
            {
                var tierMatches = connection.Query<DatEntry>(sql, new { hash }).ToList();
                if (tierMatches.Count > 0)
                {
                    candidates = tierMatches;
                    break;
                }
            }

            if (candidates == null || candidates.Count == 0)
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

            if (matchedFileIds != null)
            {
                return candidates
                    .OrderByDescending(c => matchedFileIds.Contains(c.DatFileId) ? 1 : 0)
                    .First();
            }

            return candidates[0];
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
