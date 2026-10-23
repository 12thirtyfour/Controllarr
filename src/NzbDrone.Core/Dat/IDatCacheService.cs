using System.Collections.Generic;

namespace NzbDrone.Core.Dat
{
    public interface IDatCacheService
    {
        DatFile Insert(DatFile datFile, List<DatEntry> datEntries);

        DatEntry FindByCrc32(string crc32);
        DatEntry FindBySha1(string sha1);
        DatEntry FindByMd5(string md5);
        DatEntry FindBySha256(string sha256);
    }
}
