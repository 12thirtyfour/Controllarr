using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public class MissingGameSearchCommand : Command
    {
        public int? SeriesId { get; set; }
        public bool Monitored { get; set; }
        public List<int> SeriesIds { get; set; } = [];
        public List<int> QualityProfileIds { get; set; } = [];
        public List<SeriesTypes> SeriesType { get; set; } = [];
        public HashSet<int> SeriesTags { get; set; } = [];

        public override bool SendUpdatesToClient => true;

        public MissingGameSearchCommand()
        {
            Monitored = true;
        }

        public MissingGameSearchCommand(int seriesId)
        {
            SeriesId = seriesId;
            Monitored = true;
        }
    }
}
