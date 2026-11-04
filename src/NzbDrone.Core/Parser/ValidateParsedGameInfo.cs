using NLog;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public static class ValidateParsedGameInfo
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(ValidateParsedGameInfo));

        public static bool ValidateForPlatformType(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool warnIfInvalid = true)
        {
            if (parsedEpisodeInfo.IsDaily && series.SeriesType == SeriesTypes.Standard)
            {
                var message = $"Found daily-style release for non-daily platform: {series}";

                if (warnIfInvalid)
                {
                    Logger.Warn(message);
                }
                else
                {
                    Logger.Debug(message);
                }

                return false;
            }

            return true;
        }

        public static bool ValidateForSeriesType(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool warnIfInvalid = true)
        {
            return ValidateForPlatformType(parsedEpisodeInfo, series, warnIfInvalid);
        }
    }
}
