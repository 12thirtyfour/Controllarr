using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NzbDrone.Core.Dat
{
    public class DatParserService : IDatParserService
    {
        private static readonly Regex ClrHeaderBlockRegex = new Regex(
            @"clrmamepro\s*\((?<body>[\s\S]*?)\n\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ClrGameBlockRegex = new Regex(
            @"(?:game|machine)\s*\((?<body>[\s\S]*?)\n\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ClrRomBlockRegex = new Regex(
            @"rom\s*\((?<rombody>(?:""[^""]*""|[^""\)])*)\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<string, Regex> ClrQuotedFieldRegexCache = new ConcurrentDictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Regex> ClrTokenRegexCache = new ConcurrentDictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex ProseSentenceRegex = new Regex(
            @"[.!?]\s+(?:The|A|An|In|It|This|What|When|Where|With|Thanks|After|Before|You|Your|He|She|They|Set|Based|As|On|To|For|From|By)\b",
            RegexOptions.Compiled);

        public static bool LooksLikeProseDescription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed.StartsWith(",", StringComparison.Ordinal) ||
                trimmed.Length > 140 ||
                trimmed.EndsWith("...", StringComparison.Ordinal) ||
                (trimmed.Contains("...", StringComparison.Ordinal) && trimmed.Length > 40))
            {
                return true;
            }

            if (trimmed.Contains(" is a ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(" is an ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(" developed by ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(" published by ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("? ", StringComparison.Ordinal))
            {
                return true;
            }

            return ProseSentenceRegex.IsMatch(trimmed);
        }

        private static string ResolveEffectiveGameName(string name, string description)
        {
            var trimmedName = name?.Trim();
            var trimmedDescription = description?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedName))
            {
                if (IsArcadeRomSetToken(trimmedName) &&
                    !string.IsNullOrWhiteSpace(trimmedDescription) &&
                    !LooksLikeProseDescription(trimmedDescription))
                {
                    return trimmedDescription;
                }

                return trimmedName;
            }

            if (!string.IsNullOrWhiteSpace(trimmedDescription) && !LooksLikeProseDescription(trimmedDescription))
            {
                return trimmedDescription;
            }

            return null;
        }

        private static bool IsArcadeRomSetToken(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   !name.Contains(' ') &&
                   !name.Contains('(') &&
                   name.Length <= 32;
        }

        public (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream)
        {
            if (stream == null)
            {
                return (new DatFile { CreatedAt = DateTime.UtcNow }, new List<DatEntry>());
            }

            using var streamReader = new StreamReader(stream, leaveOpen: true);
            return ParseReader(streamReader);
        }

        public (DatFile DatFile, List<DatEntry> DatEntries) Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return (new DatFile { CreatedAt = DateTime.UtcNow }, new List<DatEntry>());
            }

            using var textReader = new StringReader(content);
            return ParseReader(textReader);
        }

        private static bool IsBiosMetadata(string isBiosFlag, string category)
        {
            if (!string.IsNullOrWhiteSpace(isBiosFlag) &&
                (isBiosFlag.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                 isBiosFlag.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                 isBiosFlag.Equals("1", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(category) &&
                (category.Equals("BIOS", StringComparison.OrdinalIgnoreCase) ||
                 category.Equals("Firmware", StringComparison.OrdinalIgnoreCase) ||
                 category.Equals("Boot ROM", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static string ApplyBiosPrefixIfNeeded(string gameName, bool isBios)
        {
            if (!isBios || string.IsNullOrWhiteSpace(gameName) || gameName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase))
            {
                return gameName;
            }

            return $"[BIOS] {gameName}";
        }

        private static (DatFile DatFile, List<DatEntry> DatEntries) ParseReader(TextReader streamReader)
        {
            var datFile = new DatFile { CreatedAt = DateTime.UtcNow };
            var datEntries = new List<DatEntry>();

            // Skip leading whitespace to determine if content is XML
            int next;
            while ((next = streamReader.Peek()) != -1 && char.IsWhiteSpace((char)next))
            {
                streamReader.Read();
            }

            if (next == -1)
            {
                return (datFile, datEntries);
            }

            if (next != '<')
            {
                var content = streamReader.ReadToEnd();
                ParseClrMamePro(content, datFile, datEntries);
                datEntries.TrimExcess();
                return (datFile, datEntries);
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                CloseInput = false
            };

            using (var reader = XmlReader.Create(streamReader, settings))
            {
                var hashIndex = new Dictionary<string, DatEntry>(StringComparer.OrdinalIgnoreCase);
                reader.ReadToFollowing("datafile");

                if (reader.ReadToDescendant("header"))
                {
                    var headerEl = (XElement)XNode.ReadFrom(reader);
                    datFile.Name = ((string)headerEl.Element("name"))?.Trim();
                    datFile.Description = ((string)headerEl.Element("description"))?.Trim();
                    datFile.Version = ((string)headerEl.Element("version"))?.Trim();
                    datFile.Author = ((string)headerEl.Element("author"))?.Trim();
                    datFile.Category = ((string)headerEl.Element("category"))?.Trim();
                }

                while (!reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element && (reader.Name == "game" || reader.Name == "machine"))
                    {
                        var gameEl = (XElement)XNode.ReadFrom(reader);
                        var localGameName = (string)gameEl.Attribute("name");
                        var currentGameDescription = ((string)gameEl.Element("description"))?.Trim();
                        var isBiosAttr = ((string)gameEl.Attribute("isbios"))?.Trim();
                        var categoryAttr = ((string)gameEl.Element("category"))?.Trim();
                        var isBiosEntry = IsBiosMetadata(isBiosAttr, categoryAttr);

                        var effectiveGameName = ApplyBiosPrefixIfNeeded(
                            ResolveEffectiveGameName(localGameName, currentGameDescription),
                            isBiosEntry);

                        foreach (var rom in gameEl.Elements("rom"))
                        {
                            var romName = (string)rom.Attribute("name");
                            if (!string.IsNullOrWhiteSpace(romName) && romName.EndsWith(".unh", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var entry = new DatEntry
                            {
                                GameName = effectiveGameName,
                                RomName = romName,
                                Size = long.TryParse((string)rom.Attribute("size"), out var size) ? size : 0,
                                Crc32 = (string)rom.Attribute("crc"),
                                Md5 = (string)rom.Attribute("md5"),
                                Sha1 = (string)rom.Attribute("sha1"),
                                Sha256 = (string)rom.Attribute("sha256"),
                                Status = (string)rom.Attribute("status")
                            };
                            AddOrMergeEntry(datEntries, hashIndex, entry);
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
            }

            datEntries.TrimExcess();
            return (datFile, datEntries);
        }

        private static void ParseClrMamePro(string content, DatFile datFile, List<DatEntry> datEntries)
        {
            var hashIndex = new Dictionary<string, DatEntry>(StringComparer.OrdinalIgnoreCase);
            var headerMatch = ClrHeaderBlockRegex.Match(content);
            if (headerMatch.Success)
            {
                var headerBody = headerMatch.Groups["body"].Value;
                datFile.Name = ExtractClrField(headerBody, "name");
                datFile.Description = ExtractClrField(headerBody, "description");
                datFile.Version = ExtractClrField(headerBody, "version");
                datFile.Author = ExtractClrField(headerBody, "author") ?? "No-Intro / Libretro";
                datFile.Category = ExtractClrField(headerBody, "category") ?? "DAT";
            }

            foreach (Match gameMatch in ClrGameBlockRegex.Matches(content))
            {
                var body = gameMatch.Groups["body"].Value;
                var gameHeaderBody = ClrRomBlockRegex.Replace(body, string.Empty);
                var isBiosToken = ExtractClrToken(gameHeaderBody, "isbios");
                var categoryField = ExtractClrField(gameHeaderBody, "category");
                var isBiosEntry = IsBiosMetadata(isBiosToken, categoryField);
                var gameName = ApplyBiosPrefixIfNeeded(
                    ResolveEffectiveGameName(
                        ExtractClrField(gameHeaderBody, "name"),
                        ExtractClrField(gameHeaderBody, "description")),
                    isBiosEntry);

                foreach (Match romMatch in ClrRomBlockRegex.Matches(body))
                {
                    var romBody = romMatch.Groups["rombody"].Value;
                    var romName = ExtractClrField(romBody, "name");
                    if (string.IsNullOrWhiteSpace(romName) && string.IsNullOrWhiteSpace(gameName))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(romName) && romName.EndsWith(".unh", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var sizeStr = ExtractClrToken(romBody, "size");
                    var crc = ExtractClrToken(romBody, "crc");
                    var md5 = ExtractClrToken(romBody, "md5");
                    var sha1 = ExtractClrToken(romBody, "sha1");
                    var sha256 = ExtractClrToken(romBody, "sha256");
                    var status = ExtractClrToken(romBody, "status");

                    var baseRomName = !string.IsNullOrWhiteSpace(romName) ? Path.GetFileNameWithoutExtension(romName) : null;
                    var effectiveGameName = !string.IsNullOrWhiteSpace(gameName) ? gameName : baseRomName;

                    if (!string.IsNullOrWhiteSpace(baseRomName) &&
                        (baseRomName.Contains("(Aftermarket)", StringComparison.OrdinalIgnoreCase) ||
                         baseRomName.Contains("(Unl)", StringComparison.OrdinalIgnoreCase) ||
                         baseRomName.Contains("(Pirate)", StringComparison.OrdinalIgnoreCase) ||
                         baseRomName.Contains("(Homebrew)", StringComparison.OrdinalIgnoreCase)))
                    {
                        effectiveGameName = baseRomName;
                    }

                    AddOrMergeEntry(datEntries, hashIndex, new DatEntry
                    {
                        GameName = effectiveGameName,
                        RomName = !string.IsNullOrWhiteSpace(romName) ? romName : gameName,
                        Size = long.TryParse(sizeStr, out var size) ? size : 0L,
                        Crc32 = crc,
                        Md5 = md5,
                        Sha1 = sha1,
                        Sha256 = sha256,
                        Status = status
                    });
                }
            }
        }

        private static void AddOrMergeEntry(
            List<DatEntry> datEntries,
            Dictionary<string, DatEntry> hashIndex,
            DatEntry incoming)
        {
            if (incoming == null)
            {
                return;
            }

            DatEntryHashHelper.NormalizeEntryHashes(incoming);

            if (DatEntryHashHelper.HasAnyHash(incoming) &&
                DatEntryHashHelper.TryFindMatchingEntry(hashIndex, incoming, out var existing))
            {
                DatEntryHashHelper.MergeDatEntry(existing, incoming, hashIndex);
                return;
            }

            datEntries.Add(incoming);
            DatEntryHashHelper.IndexEntryHashes(hashIndex, incoming);
        }

        private static string ExtractClrField(string text, string fieldName)
        {
            var quotedRegex = ClrQuotedFieldRegexCache.GetOrAdd(
                fieldName,
                f => new Regex($@"\b{Regex.Escape(f)}\s+""(?<val>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled));
            var quotedMatch = quotedRegex.Match(text);
            if (quotedMatch.Success)
            {
                return quotedMatch.Groups["val"].Value.Trim();
            }

            return ExtractClrToken(text, fieldName);
        }

        private static string ExtractClrToken(string text, string fieldName)
        {
            var tokenRegex = ClrTokenRegexCache.GetOrAdd(
                fieldName,
                f => new Regex($@"\b{Regex.Escape(f)}\s+(?<val>[^\s""\)]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled));
            var tokenMatch = tokenRegex.Match(text);
            return tokenMatch.Success ? tokenMatch.Groups["val"].Value.Trim() : null;
        }
    }
}
