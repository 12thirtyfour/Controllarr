using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public class PlatformEditedService : IHandle<PlatformEditedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;

        public PlatformEditedService(IManageCommandQueue commandQueueManager)
        {
            _commandQueueManager = commandQueueManager;
        }

        public void Handle(PlatformEditedEvent message)
        {
            if (message.Series.SeriesType != message.OldSeries.SeriesType ||
                message.Series.Language != message.OldSeries.Language ||
                message.Series.SeasonType != message.OldSeries.SeasonType)
            {
                _commandQueueManager.Push(new RefreshPlatformCommand([message.Series.Id], false));
            }
        }
    }
}
