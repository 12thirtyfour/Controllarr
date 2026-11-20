using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Dat
{
    public class DatFile : ModelBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
