using System.Collections.Generic;

namespace NzbDrone.Core.Dat
{
    public interface IDatCacheService
    {
        DatFile Insert(DatFile datFile, List<DatEntry> datEntries);
        List<DatFile> GetAllDatFiles();
        List<DatEntry> GetEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null);

        DatEntry FindByCrc32(string crc32);
        DatEntry FindBySha1(string sha1);
        DatEntry FindByMd5(string md5);
        DatEntry FindBySha256(string sha256);
        List<DatEntry> SearchByName(string query, int limit = 50);
    }
}
