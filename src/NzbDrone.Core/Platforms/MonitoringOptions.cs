using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Tv
{
    public class MonitoringOptions : IEmbeddedDocument
    {
        public bool IgnoreEpisodesWithFiles { get; set; }
        public bool IgnoreGamesWithFiles { get => IgnoreEpisodesWithFiles; set => IgnoreEpisodesWithFiles = value; }
        public bool IgnoreEpisodesWithoutFiles { get; set; }
        public bool IgnoreGamesWithoutFiles { get => IgnoreEpisodesWithoutFiles; set => IgnoreEpisodesWithoutFiles = value; }
        public MonitorTypes Monitor { get; set; }
    }

    public enum MonitorTypes
    {
        Unknown,
        All,
        Future,
        Missing,
        Existing,
        FirstSeason,
        FirstRegion = FirstSeason,
        LastSeason,
        LastRegion = LastSeason,

        [Obsolete]
        LatestSeason,

        Pilot,
        FirstGame = Pilot,
        Recent,
        MonitorSpecials,
        UnmonitorSpecials,
        None,
        Skip
    }

    public enum NewItemMonitorTypes
    {
        All,
        None
    }
}
