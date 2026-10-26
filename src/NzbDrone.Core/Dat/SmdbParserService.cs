using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Dat
{
    public class SmdbParserService : ISmdbParserService
    {
        public (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream, string name = "SMDB")
        {
            var datFile = new DatFile
            {
                Name = string.IsNullOrWhiteSpace(name) ? "SMDB" : name.Trim(),
                Description = "Hardware Target Game Database (SMDB)",
                Author = "HTGDB",
                Category = "SMDB",
                CreatedAt = DateTime.UtcNow
            };

            var datEntries = new List<DatEntry>();

            if (stream == null)
            {
                return (datFile, datEntries);
            }

            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var columns = line.Split('\t');
                    if (columns.Length < 2)
                    {
                        continue;
                    }

                    var sha256 = NormalizeHash(columns[0]);
                    var smdbPath = columns[1]?.Trim();

                    if (string.IsNullOrWhiteSpace(smdbPath))
                    {
                        continue;
                    }

                    var normalizedRelativePath = smdbPath.Replace('\\', '/');
                    var romName = Path.GetFileName(normalizedRelativePath);
                    var cleanRomName = RomParser.CleanIndexPrefix(romName);
                    var gameName = Path.GetFileNameWithoutExtension(cleanRomName);
                    var cleanGameName = RomParser.CleanIndexPrefix(gameName);
                    var parsedRom = RomParser.ParseRomName(cleanRomName);
                    var region = parsedRom?.Regions?.FirstOrDefault();

                    var sha1 = columns.Length > 2 ? NormalizeHash(columns[2]) : null;
                    var md5 = columns.Length > 3 ? NormalizeHash(columns[3]) : null;
                    var crc32 = columns.Length > 4 ? NormalizeHash(columns[4]) : null;
                    var size = columns.Length > 5 && long.TryParse(columns[5]?.Trim(), out var parsedSize) ? parsedSize : 0L;

                    datEntries.Add(new DatEntry
                    {
                        GameName = !string.IsNullOrWhiteSpace(cleanGameName) ? cleanGameName : cleanRomName,
                        RomName = !string.IsNullOrWhiteSpace(cleanRomName) ? cleanRomName : romName,
                        SmdbPath = smdbPath,
                        Size = size,
                        Sha256 = sha256,
                        Sha1 = sha1,
                        Md5 = md5,
                        Crc32 = crc32,
                        Region = region,
                        Status = "verified"
                    });
                }
            }

            return (datFile, datEntries);
        }

        private static string NormalizeHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToLowerInvariant();
        }
    }
}
