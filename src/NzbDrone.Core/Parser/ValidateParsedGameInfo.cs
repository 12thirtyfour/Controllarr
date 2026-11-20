using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public static class ValidateParsedGameInfo
    {
        public static bool ValidateForPlatformType(ParsedEpisodeInfo parsedGameInfo, Series platform, bool warnIfInvalid = true)
        {
            // Date-stamped releases (prototypes, builds, dated collections) are valid for all console platforms.
            return parsedGameInfo != null && platform != null;
        }

        public static bool ValidateForSeriesType(ParsedEpisodeInfo parsedGameInfo, Series platform, bool warnIfInvalid = true)
        {
            return ValidateForPlatformType(parsedGameInfo, platform, warnIfInvalid);
        }
    }
}
