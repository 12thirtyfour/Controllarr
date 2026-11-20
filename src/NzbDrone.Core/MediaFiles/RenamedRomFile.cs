namespace NzbDrone.Core.MediaFiles
{
    public class RenamedRomFile
    {
        public EpisodeFile EpisodeFile { get; set; }
        public string PreviousPath { get; set; }
        public string PreviousRelativePath { get; set; }
    }
}
