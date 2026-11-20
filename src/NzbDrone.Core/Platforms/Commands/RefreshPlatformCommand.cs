using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.Commands
{
    public class RefreshPlatformCommand : Command
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SeriesId
        {
            get => 0;
            set
            {
                if (SeriesIds.Empty())
                {
                    SeriesIds.Add(value);
                }
            }
        }

        public List<int> SeriesIds { get; set; }
        public bool IsNewSeries { get; set; }

        public RefreshPlatformCommand()
        {
            SeriesIds = new List<int>();
        }

        public RefreshPlatformCommand(List<int> seriesIds, bool isNewSeries = false)
        {
            SeriesIds = seriesIds;
            IsNewSeries = isNewSeries;
        }

        public RefreshPlatformCommand(int seriesId, bool isNewSeries = false)
        {
            SeriesIds = new List<int> { seriesId };
            IsNewSeries = isNewSeries;
        }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => SeriesIds.Empty();

        public override bool IsLongRunning => true;

        public override string CompletionMessage => "Completed";
    }
}
