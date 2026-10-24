using System.Collections.Generic;

namespace NzbDrone.Core.Dat
{
    public interface IDatCacheService
    {
        DatFile Insert(DatFile datFile, List<DatEntry> datEntries);
        List<DatFile> GetAllDatFiles();

        DatEntry FindByCrc32(string crc32);
        DatEntry FindBySha1(string sha1);
        DatEntry FindByMd5(string md5);
        DatEntry FindBySha256(string sha256);
        List<DatEntry> SearchByName(string query, int limit = 50);
    }
}
