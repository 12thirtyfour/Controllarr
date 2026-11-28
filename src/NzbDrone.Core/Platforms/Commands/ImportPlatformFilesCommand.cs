using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.Commands
{
    public class ImportPlatformFilesCommand : Command
    {
        public int SeriesId { get; set; }
        public List<string> IncomingPaths { get; set; }
        public bool PopulateLibraryInfo { get; set; }

        public override bool SendUpdatesToClient => true;

        public override bool RequiresDiskAccess => true;

        public ImportPlatformFilesCommand()
        {
            IncomingPaths = new List<string>();
        }

        public ImportPlatformFilesCommand(int seriesId, List<string> incomingPaths, bool populateLibraryInfo = false)
        {
            SeriesId = seriesId;
            IncomingPaths = incomingPaths ?? new List<string>();
            PopulateLibraryInfo = populateLibraryInfo;
        }
    }
}
