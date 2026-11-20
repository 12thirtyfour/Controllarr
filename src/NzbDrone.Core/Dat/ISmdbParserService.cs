using System.Collections.Generic;
using System.IO;

namespace NzbDrone.Core.Dat
{
    public interface ISmdbParserService
    {
        (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream, string name = "SMDB");
    }
}
