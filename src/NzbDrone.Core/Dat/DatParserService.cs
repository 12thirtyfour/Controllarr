using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Dat
{
    public class DatParserService : IDatParserService
    {
        private readonly Logger _logger = NzbDroneLogger.GetLogger(typeof(DatParserService));

        public (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream)
        {
            var datFile = new DatFile { CreatedAt = DateTime.UtcNow };
            var datEntries = new List<DatEntry>();

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };

            using (var reader = XmlReader.Create(stream, settings))
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
    }
}
