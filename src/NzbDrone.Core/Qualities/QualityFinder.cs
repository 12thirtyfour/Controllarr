using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Qualities
{
    public static class QualityFinder
    {
        public static Quality FindBySourceAndResolution(QualitySource source, int resolution)
        {
            if (source == QualitySource.Unknown)
            {
                return Quality.Unknown;
            }

            return Quality.All.FirstOrDefault(q => q.Source == source) ?? Quality.Unknown;
        }

        public static Quality FindByDumpTag(string dumpTag)
        {
            if (string.IsNullOrWhiteSpace(dumpTag))
            {
                return Quality.Unknown;
            }

            var cleanTag = dumpTag.Trim('[', ']', '(', ')', ' ');

            if (cleanTag.Equals("Redump", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.VerifiedRedump;
            }

            if (cleanTag.Equals("No-Intro", StringComparison.OrdinalIgnoreCase) ||
                cleanTag.Equals("NoIntro", StringComparison.OrdinalIgnoreCase))
            {
                return Quality.NoIntroVerified;
            }

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
