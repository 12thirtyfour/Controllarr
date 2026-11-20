using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Exceptions
{
    public class PlatformNotFoundException : NzbDroneException
    {
        public int TvdbSeriesId { get; set; }

        public PlatformNotFoundException(int tvdbSeriesId)
            : base(string.Format("Platform or game with catalog ID {0} was not found in the metadata catalog.", tvdbSeriesId))
        {
            TvdbSeriesId = tvdbSeriesId;
        }

        public PlatformNotFoundException(int tvdbSeriesId, string message, params object[] args)
            : base(message, args)
        {
            TvdbSeriesId = tvdbSeriesId;
        }

        public PlatformNotFoundException(int tvdbSeriesId, string message)
            : base(message)
        {
            TvdbSeriesId = tvdbSeriesId;
        }
    }

    public class SeriesNotFoundException : PlatformNotFoundException
    {
        public SeriesNotFoundException(int tvdbSeriesId)
            : base(tvdbSeriesId)
        {
        }

        public SeriesNotFoundException(int tvdbSeriesId, string message, params object[] args)
            : base(tvdbSeriesId, message, args)
        {
        }

        public SeriesNotFoundException(int tvdbSeriesId, string message)
            : base(tvdbSeriesId, message)
        {
        }
    }
}
