using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles
{
    public class RomFileMoveResult
    {
        public RomFileMoveResult()
        {
            OldFiles = new List<DeletedRomFile>();
        }

        public EpisodeFile EpisodeFile { get; set; }
        public List<DeletedRomFile> OldFiles { get; set; }
    }
}
