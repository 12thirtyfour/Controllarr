using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser;

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

        private static readonly HashSet<string> KnownPlatformSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "n64", "64dd", "gamecube", "wii", "wiiu",
            "gb", "gbc", "gba", "nds", "3ds", "switch",
            "genesis", "segacd", "32x", "saturn", "dreamcast", "sms", "gg",
            "tg16", "tgcd",
            "ps1", "ps2", "psp", "ps3", "psvita",
            "xbox", "xbox360",
            "atari2600", "atari7800", "lynx", "jaguar", "jaguarcd",
            "neogeo", "ngpc", "wonderswan", "vb"
        };

        public List<DatEntry> GetEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null)
        {
            var slug = (platformSlug ?? string.Empty).Trim().ToLowerInvariant();
            var name = (platformName ?? slug).Trim().ToLowerInvariant();
            var sys = (libretroSystemName ?? name).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(slug) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(sys))
            {
                return new List<DatEntry>();
            }

            using (var connection = _database.OpenConnection())
            {
                var datFiles = connection.Query<DatFile>(@"SELECT ""Id"", ""Name"", ""Description"" FROM ""DatFiles""").ToList();
                var matchedFileIds = new List<int>();

                foreach (var file in datFiles)
                {
                    if (IsDatFilePlatformMatch(file, slug, name, sys))
                    {
                        matchedFileIds.Add(file.Id);
                    }
                }

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
                    return datFileName.Contains("wii u") || datFileName.Contains("wiiu");
                case "sms":
                    return datFileName.Contains("genesis") || datFileName.Contains("mega drive") || datFileName.Contains("sega cd") || datFileName.Contains("game gear");
                case "tg16":
                    return datFileName.Contains("tgcd") || datFileName.Contains("turbografx-cd") || datFileName.Contains("pc engine cd");
                default:
                    return false;
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

        public DatFile GetDatFileById(int id)
        {
            var sql = @"SELECT * FROM ""DatFiles"" WHERE ""Id"" = @id LIMIT 1";

            using (var connection = _database.OpenConnection())
            {
                return connection.QueryFirstOrDefault<DatFile>(sql, new { id });
            }
        }

        public DatEntryWithPlatform FindPlatformByHash(string crc32, string sha1 = null, string md5 = null, string sha256 = null)
        {
            DatEntry entry = null;

            if (!string.IsNullOrWhiteSpace(sha256))
            {
                entry = FindBySha256(sha256);
            }

            if (entry == null && !string.IsNullOrWhiteSpace(sha1))
            {
                entry = FindBySha1(sha1);
            }

            if (entry == null && !string.IsNullOrWhiteSpace(md5))
            {
                entry = FindByMd5(md5);
            }

            if (entry == null && !string.IsNullOrWhiteSpace(crc32))
            {
                entry = FindByCrc32(crc32);
            }

            if (entry == null)
            {
                return null;
            }

            var datFile = GetDatFileById(entry.DatFileId);
            var platformSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(null, datFile?.Name ?? datFile?.Description);

            return new DatEntryWithPlatform
            {
                DatEntry = entry,
                DatFile = datFile,
                PlatformSlug = platformSlug
            };
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
