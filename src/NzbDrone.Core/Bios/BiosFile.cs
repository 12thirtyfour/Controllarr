using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Bios
{
    public class BiosFile : ModelBase
    {
        public string GamePlatform { get; set; }
        public string PlatformTitle { get; set; }
        public string FileName { get; set; }
        public string ExpectedMd5 { get; set; }
        public string ExpectedSha1 { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public bool IsVerified { get; set; }
        public int Status { get; set; }
        public string ResolvedPath { get; set; }
        public string DownloadUrl { get; set; }
    }
}
