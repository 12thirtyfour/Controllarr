using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Dat
{
    public class DatEntry : ModelBase
    {
        public int DatFileId { get; set; }
        public string GameName { get; set; }
        public string RomName { get; set; }
        public long Size { get; set; }
        public string Crc32 { get; set; }
        public string Md5 { get; set; }
        public string Sha1 { get; set; }
        public string Region { get; set; }
        public string Status { get; set; }
    }
}
