using System.Collections.Generic;
using System.IO;

namespace NzbDrone.Core.Dat
{
    public interface IDatParserService
    {
        (DatFile DatFile, List<DatEntry> DatEntries) Parse(Stream stream);
    }
}
