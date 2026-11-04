using System.Collections.Generic;

namespace NzbDrone.Core.Tv
{
    public static class PlatformTitleNormalizer
    {
        private static readonly Dictionary<int, string> PreComputedTitles = new()
        {
            { 281588, "a to z" },
        };

        public static string Normalize(string title, int tvdbId)
        {
            if (PreComputedTitles.TryGetValue(tvdbId, out var value))
            {
                return value;
            }

            return Parser.Parser.NormalizeTitle(title).ToLower();
        }
    }

    public static class SeriesTitleNormalizer
    {
        public static string Normalize(string title, int tvdbId) => PlatformTitleNormalizer.Normalize(title, tvdbId);
    }
}
