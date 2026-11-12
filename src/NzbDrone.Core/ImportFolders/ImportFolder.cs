using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.ImportFolders
{
    public class ImportFolder : ModelBase
    {
        public string Path { get; set; }
        public bool Accessible { get; set; }
        public bool IsEmpty { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }
        public bool ContainsRomFiles { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }
}
