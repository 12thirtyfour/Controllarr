using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Parser
{
    public class InvalidRegionException : NzbDroneException
    {
        public InvalidRegionException(string message, params object[] args)
            : base(message, args)
        {
        }

        public InvalidRegionException(string message)
            : base(message)
        {
        }
    }
}
