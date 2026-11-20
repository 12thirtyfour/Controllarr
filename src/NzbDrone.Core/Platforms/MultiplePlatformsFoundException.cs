using System.Collections.Generic;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Tv
{
    public class MultiplePlatformsFoundException : NzbDroneException
    {
        public List<Series> Series { get; set; }

        public MultiplePlatformsFoundException(List<Series> series, string message, params object[] args)
            : base(message, args)
        {
            Series = series;
        }
    }

    public class MultipleSeriesFoundException : MultiplePlatformsFoundException
    {
        public MultipleSeriesFoundException(List<Series> series, string message, params object[] args)
            : base(series, message, args)
        {
        }
    }
}
