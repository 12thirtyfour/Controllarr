using System.Collections.Generic;

namespace NzbDrone.Core.Dat
{
    public class DatEntryWithPlatform
    {
        public DatEntry DatEntry { get; set; }
        public DatFile DatFile { get; set; }
        public string PlatformSlug { get; set; }
    }

    public interface IDatCacheService
    {
        DatFile Insert(DatFile datFile, List<DatEntry> datEntries);
        List<DatFile> GetAllDatFiles();
        DatFile GetDatFileById(int id);
        List<DatEntry> GetEntriesForPlatform(string platformSlug, string platformName = null, string libretroSystemName = null);

        DatEntry FindByCrc32(string crc32);
        DatEntry FindBySha1(string sha1);
        DatEntry FindByMd5(string md5);
        DatEntry FindBySha256(string sha256);
        DatEntryWithPlatform FindPlatformByHash(string crc32, string sha1 = null, string md5 = null, string sha256 = null);
        List<DatEntry> SearchByName(string query, int limit = 50);
    }
}
