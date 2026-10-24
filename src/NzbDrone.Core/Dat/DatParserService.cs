using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Instrumentation;

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
            @"rom\s*\((?<rombody>[^\)]*)\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Logger _logger = NzbDroneLogger.GetLogger(typeof(DatParserService));

        public (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream)
        {
            var datFile = new DatFile { CreatedAt = DateTime.UtcNow };
            var datEntries = new List<DatEntry>();

            if (stream == null)
            {
                return (datFile, datEntries);
            }

            string content;
            using (var streamReader = new StreamReader(stream, leaveOpen: true))
            {
                content = streamReader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return (datFile, datEntries);
            }

            var trimmed = content.TrimStart();
            if (!trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                ParseClrMamePro(content, datFile, datEntries);
                return (datFile, datEntries);
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };

            using (var stringReader = new StringReader(content))
            using (var reader = XmlReader.Create(stringReader, settings))
            {
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

                        var effectiveGameName = !string.IsNullOrEmpty(currentGameDescription) ? currentGameDescription : localGameName;

                        foreach (var rom in gameEl.Elements("rom"))
                        {
                            var entry = new DatEntry
                            {
                                GameName = effectiveGameName,
                                RomName = (string)rom.Attribute("name"),
                                Size = long.TryParse((string)rom.Attribute("size"), out var size) ? size : 0,
                                Crc32 = ((string)rom.Attribute("crc"))?.ToLowerInvariant(),
                                Md5 = ((string)rom.Attribute("md5"))?.ToLowerInvariant(),
                                Sha1 = ((string)rom.Attribute("sha1"))?.ToLowerInvariant(),
                                Status = (string)rom.Attribute("status")
                            };
                            datEntries.Add(entry);
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
            }

            return (datFile, datEntries);
        }

        private static void ParseClrMamePro(string content, DatFile datFile, List<DatEntry> datEntries)
        {
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
                var gameName = ExtractClrField(body, "description") ?? ExtractClrField(body, "name");

                foreach (Match romMatch in ClrRomBlockRegex.Matches(body))
                {
                    var romBody = romMatch.Groups["rombody"].Value;
                    var romName = ExtractClrField(romBody, "name");
                    if (string.IsNullOrWhiteSpace(romName) && string.IsNullOrWhiteSpace(gameName))
                    {
                        continue;
                    }

                    var sizeStr = ExtractClrToken(romBody, "size");
                    var crc = ExtractClrToken(romBody, "crc")?.ToLowerInvariant();
                    var md5 = ExtractClrToken(romBody, "md5")?.ToLowerInvariant();
                    var sha1 = ExtractClrToken(romBody, "sha1")?.ToLowerInvariant();
                    var sha256 = ExtractClrToken(romBody, "sha256")?.ToLowerInvariant();
                    var status = ExtractClrToken(romBody, "status");

                    datEntries.Add(new DatEntry
                    {
                        GameName = !string.IsNullOrWhiteSpace(gameName) ? gameName : Path.GetFileNameWithoutExtension(romName),
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

        private static string ExtractClrField(string text, string fieldName)
        {
            var quotedMatch = Regex.Match(text, $@"\b{Regex.Escape(fieldName)}\s+""(?<val>[^""]*)""", RegexOptions.IgnoreCase);
            if (quotedMatch.Success)
            {
                return quotedMatch.Groups["val"].Value.Trim();
            }

            return ExtractClrToken(text, fieldName);
        }

        private static string ExtractClrToken(string text, string fieldName)
        {
            var tokenMatch = Regex.Match(text, $@"\b{Regex.Escape(fieldName)}\s+(?<val>[^\s""\)]+)", RegexOptions.IgnoreCase);
            return tokenMatch.Success ? tokenMatch.Groups["val"].Value.Trim() : null;
        }
    }
}
