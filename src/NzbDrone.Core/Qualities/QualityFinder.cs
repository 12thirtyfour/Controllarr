using System;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Qualities
{
    public static class QualityFinder
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(QualityFinder));

        // Legacy video resolution and broadcast sources for backwards compatibility with tests. For gaming ROMs, use FindByDumpTag instead.
        public static Quality FindBySourceAndResolution(QualitySource source, int resolution)
        {
            if (source == QualitySource.Unknown && resolution == 0)
            {
                return Quality.Unknown;
            }

            var matchingQuality = Quality.All.FirstOrDefault(q => q.Source == source && q.Resolution == resolution);

            if (matchingQuality != null)
            {
                return matchingQuality;
            }

            // Handle legacy 576p video releases so they don't roll up incorrectly in tests
            if (resolution < 720)
            {
                switch (source)
                {
                    case QualitySource.Television:
                        return Quality.SDTV;
                    case QualitySource.Web:
                        return Quality.WEBDL480p;
                    case QualitySource.WebRip:
                        return Quality.WEBRip480p;
                }
            }

            var matchingResolution = Quality.All.Where(q => q.Resolution == resolution)
                                            .OrderBy(q => q.Source)
                                            .ToList();

            var nearestQuality = Quality.Unknown;

            foreach (var quality in matchingResolution)
            {
                if (quality.Source >= source)
                {
                    nearestQuality = quality;
                    break;
                }
            }

            Logger.Warn("Unable to find exact quality for {0} and {1}. Using {2} as fallback", source, resolution, nearestQuality);

            return nearestQuality;
        }

        public static Quality FindByDumpTag(string dumpTag)
        {
            if (string.IsNullOrWhiteSpace(dumpTag))
            {
                return Quality.Unknown;
            }

            var cleanTag = dumpTag.Trim('[', ']', '(', ')', ' ');

            if (cleanTag.Equals("!", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.StartsWith("!p", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("o", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Verified", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("GoodDump", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.VerifiedGoodDump;
            }

            if (cleanTag.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(cleanTag, @"^b\d+$", RegexOptions.IgnoreCase) ||
                cleanTag.Equals("bad", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Unverified", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.UnverifiedRaw;
            }

            if (cleanTag.StartsWith("T+", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.StartsWith("T-", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(cleanTag, @"^h\d+$", RegexOptions.IgnoreCase) ||
                cleanTag.Equals("hack", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Translation", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.TranslationPatch;
            }

            if (cleanTag.Equals("Beta", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Proto", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Sample", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.BetaProto;
            }

            return Quality.Unknown;
        }
    }
}
