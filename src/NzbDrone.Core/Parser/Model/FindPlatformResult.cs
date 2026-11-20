using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser.Model
{
    public class FindPlatformResult
    {
        public Series Series { get; set; }
        public PlatformMatchType MatchType { get; set; }

        public FindPlatformResult(Series series, PlatformMatchType matchType)
        {
            Series = series;
            MatchType = matchType;
        }
    }

    public enum PlatformMatchType
    {
        Unknown = 0,
        Title = 1,
        Alias = 2,
        Id = 3
    }
}
