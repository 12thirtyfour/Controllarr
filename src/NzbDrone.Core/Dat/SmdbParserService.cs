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
                    var lineSpan = line.AsSpan().Trim();
                    if (lineSpan.IsEmpty || lineSpan[0] == '#')
                    {
                        continue;
                    }

                    var tab1 = line.IndexOf('\t');
                    if (tab1 < 0)
                    {
                        continue;
                    }

                    var tab2 = line.IndexOf('\t', tab1 + 1);
                    var tab3 = tab2 >= 0 ? line.IndexOf('\t', tab2 + 1) : -1;
                    var tab4 = tab3 >= 0 ? line.IndexOf('\t', tab3 + 1) : -1;
                    var tab5 = tab4 >= 0 ? line.IndexOf('\t', tab4 + 1) : -1;

                    var sha256Span = line.AsSpan(0, tab1).Trim();
                    var smdbPathSpan = tab2 >= 0 ? line.AsSpan(tab1 + 1, tab2 - tab1 - 1).Trim() : line.AsSpan(tab1 + 1).Trim();

                    if (smdbPathSpan.IsEmpty)
                    {
                        continue;
                    }

                    var sha256 = NormalizeHash(sha256Span);
                    var smdbPath = smdbPathSpan.ToString();

                    var normalizedRelativePath = smdbPath.Replace('\\', '/');
                    var romName = Path.GetFileName(normalizedRelativePath);
                    var cleanRomName = RomParser.CleanIndexPrefix(romName);
                    var gameName = Path.GetFileNameWithoutExtension(cleanRomName);
                    var cleanGameName = RomParser.CleanIndexPrefix(gameName);

                    if (!IsValidGameName(cleanGameName) || !IsValidGameName(cleanRomName) || !IsValidGameName(romName))
                    {
                        continue;
                    }

                    var parsedRom = RomParser.ParseRomName(cleanRomName);
                    var region = parsedRom?.Regions?.FirstOrDefault();

                    string sha1 = null;
                    if (tab2 >= 0)
                    {
                        var sha1Span = tab3 >= 0 ? line.AsSpan(tab2 + 1, tab3 - tab2 - 1).Trim() : line.AsSpan(tab2 + 1).Trim();
                        sha1 = NormalizeHash(sha1Span);
                    }

                    string md5 = null;
                    if (tab3 >= 0)
                    {
                        var md5Span = tab4 >= 0 ? line.AsSpan(tab3 + 1, tab4 - tab3 - 1).Trim() : line.AsSpan(tab3 + 1).Trim();
                        md5 = NormalizeHash(md5Span);
                    }

                    string crc32 = null;
                    if (tab4 >= 0)
                    {
                        var crcSpan = tab5 >= 0 ? line.AsSpan(tab4 + 1, tab5 - tab4 - 1).Trim() : line.AsSpan(tab4 + 1).Trim();
                        crc32 = NormalizeHash(crcSpan);
                    }

                    var size = 0L;
                    if (tab5 >= 0)
                    {
                        var sizeSpan = line.AsSpan(tab5 + 1).Trim();
                        long.TryParse(sizeSpan, out size);
                    }

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

            datEntries.TrimExcess();
            return (datFile, datEntries);
        }

        private static bool IsValidGameName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed.StartsWith("@", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmed.IndexOf("Pack Here", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (trimmed.IndexOf("Place ", StringComparison.OrdinalIgnoreCase) >= 0 && trimmed.IndexOf(" Here", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return false;
            }

            return true;
        }

        private static string NormalizeHash(ReadOnlySpan<char> span)
        {
            var trimmed = span.Trim();
            if (trimmed.IsEmpty)
            {
                return null;
            }

            var allLower = true;
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                if (char.IsAsciiLetterUpper(c))
                {
                    allLower = false;
                    break;
                }
            }

            return allLower ? trimmed.ToString() : trimmed.ToString().ToLowerInvariant();
        }
    }
}
