namespace NzbDrone.Core.MediaFiles
{
    public class DeletedRomFile
    {
        public string RecycleBinPath { get; set; }
        public EpisodeFile EpisodeFile { get; set; }

        public DeletedRomFile(EpisodeFile episodeFile, string recycleBinPath)
        {
            EpisodeFile = episodeFile;
            RecycleBinPath = recycleBinPath;
        }
    }
}
