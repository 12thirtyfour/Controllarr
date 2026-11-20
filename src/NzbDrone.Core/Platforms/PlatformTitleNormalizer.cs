using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Tv
{
    public static class PlatformTitleNormalizer
    {
        private static readonly Dictionary<int, string> PreComputedTitles = new()
        {
            { 1003, "nintendo super entertainment system" },
            { 281588, "a to z" },
        };

        public static string Normalize(string title, int tvdbId)
        {
            if (PreComputedTitles.TryGetValue(tvdbId, out var value))
            {
                return value;
            }

            if (string.Equals(title?.Trim(), "Super Nintendo Entertainment System", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(title?.Trim(), "Super Nintendo", StringComparison.OrdinalIgnoreCase))
            {
                return "nintendo super entertainment system";
            }

            return Parser.Parser.NormalizeTitle(title).ToLower();
        }
    }

    public static class SeriesTitleNormalizer
    {
        public static string Normalize(string title, int tvdbId) => PlatformTitleNormalizer.Normalize(title, tvdbId);
    }
}
