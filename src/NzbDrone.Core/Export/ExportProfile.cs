namespace NzbDrone.Core.Export
{
    public enum ExportType
    {
        Symlink = 1,
        FlashCart = 2,
        Handheld = 3
    }

    public class ExportProfile
    {
        public string Name { get; set; }
        public ExportType ExportType { get; set; }
        public string OutputDirectory { get; set; }
        public bool IncludeBiosFiles { get; set; }
    }
}
