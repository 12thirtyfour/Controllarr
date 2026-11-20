using System;

namespace NzbDrone.Common.Exceptions
{
    public class ControllarrStartupException : NzbDroneException
    {
        public ControllarrStartupException(string message, params object[] args)
            : base("Controllarr failed to start: " + string.Format(message, args))
        {
        }

        public ControllarrStartupException(string message)
            : base("Controllarr failed to start: " + message)
        {
        }

        public ControllarrStartupException()
            : base("Controllarr failed to start")
        {
        }

        public ControllarrStartupException(Exception innerException, string message, params object[] args)
            : base("Controllarr failed to start: " + string.Format(message, args), innerException)
        {
        }

        public ControllarrStartupException(Exception innerException, string message)
            : base("Controllarr failed to start: " + message, innerException)
        {
        }

        public ControllarrStartupException(Exception innerException)
            : base("Controllarr failed to start", innerException)
        {
        }
    }
}
