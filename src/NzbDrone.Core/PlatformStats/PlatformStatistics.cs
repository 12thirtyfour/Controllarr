using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.PlatformStats
{
    public class PlatformStatistics : ResultSet
    {
        public int SeriesId { get; set; }
        public DateTime? NextAiring { get; set; }
        public DateTime? PreviousAiring { get; set; }
        public DateTime? LastAired { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public int MonitoredEpisodeCount { get; set; }
        public int RomFileCount { get => EpisodeFileCount; set => EpisodeFileCount = value; }
        public int GameCount { get => EpisodeCount; set => EpisodeCount = value; }
        public int TotalGameCount { get => TotalEpisodeCount; set => TotalEpisodeCount = value; }
        public int MonitoredGameCount { get => MonitoredEpisodeCount; set => MonitoredEpisodeCount = value; }
        public long SizeOnDisk { get; set; }
        public List<string> ReleaseGroups { get; set; }
        public List<ReleaseType> ReleaseTypes { get; set; }
        public List<Quality> EpisodeFileQualities { get; set; }
        public List<Quality> RomFileQualities { get => EpisodeFileQualities; set => EpisodeFileQualities = value; }
        public List<SeasonStatistics> SeasonStatistics { get; set; }
        public List<SeasonStatistics> RegionStatistics { get => SeasonStatistics; set => SeasonStatistics = value; }
    }

    public class SeriesStatistics : PlatformStatistics
    {
    }
}
