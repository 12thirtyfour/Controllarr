namespace NzbDrone.Core.Parser.Rom
{
    public interface IRomReleaseParser
    {
        RomParsedInfo Parse(string releaseTitle);
    }
}
