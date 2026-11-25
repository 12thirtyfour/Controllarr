using System;

namespace NzbDrone.Core.IndexerSearch
{
    internal static class SearchShutdownHelper
    {
        public static bool IsContainerDisposedException(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is ObjectDisposedException ||
                    (string.Equals(current.GetType().Name, "ContainerException", StringComparison.Ordinal) &&
                     current.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
