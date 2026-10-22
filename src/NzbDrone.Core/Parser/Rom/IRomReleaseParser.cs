using System.Collections.Generic;

namespace NzbDrone.Core.Parser.Rom
{
    public interface IRomReleaseParser
    {
        RomReleaseInfo Parse(string releaseTitle);
        int CalculateRomPriority(RomReleaseInfo info, List<string> preferredRegions);
    }
}
