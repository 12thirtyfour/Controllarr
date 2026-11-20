namespace NzbDrone.Core.Parser.Model
{
    public class PlatformTitleInfo
    {
        public string Title { get; set; }
        public string TitleWithoutYear { get; set; }
        public int Year { get; set; }
        public string[] AllTitles { get; set; }
    }

    public class SeriesTitleInfo : PlatformTitleInfo
    {
    }
}
