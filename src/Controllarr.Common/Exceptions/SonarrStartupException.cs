using System;

namespace NzbDrone.Common.Exceptions
{
    public class SonarrStartupException : ControllarrStartupException
    {
        public SonarrStartupException(string message, params object[] args)
            : base(message, args)
        {
        }

        public SonarrStartupException(string message)
            : base(message)
        {
        }

        public SonarrStartupException()
            : base()
        {
        }

        public SonarrStartupException(Exception innerException, string message, params object[] args)
            : base(innerException, message, args)
        {
        }

        public SonarrStartupException(Exception innerException, string message)
            : base(innerException, message)
        {
        }

        public SonarrStartupException(Exception innerException)
            : base(innerException)
        {
        }
    }
}
