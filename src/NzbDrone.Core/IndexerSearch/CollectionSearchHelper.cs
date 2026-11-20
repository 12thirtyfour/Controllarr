using System;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.IndexerSearch
{
    internal static class CollectionSearchHelper
    {
        public static bool TryGrabCollection(
            ISearchForReleases releaseSearchService,
            IProcessDownloadDecisions processDownloadDecisions,
            Logger logger,
            int seriesId,
            bool userInvokedSearch)
        {
            try
            {
                logger.ProgressInfo("Searching for platform collection before individual ROMs...");

                var decisions = releaseSearchService
                    .PlatformCollectionSearch(seriesId, userInvokedSearch, false)
                    .GetAwaiter()
                    .GetResult();
                var processed = processDownloadDecisions
                    .ProcessDecisions(decisions)
                    .GetAwaiter()
                    .GetResult();

                if (processed.Grabbed.Count > 0)
                {
                    logger.ProgressInfo("Collection search grabbed {0} release(s); skipping individual ROM search while collection downloads.", processed.Grabbed.Count);
                    return true;
                }

                logger.ProgressInfo("No collection release grabbed; moving on to individual ROM search.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unable to search for platform collection; moving on to individual ROM search.");
            }

            return false;
        }
    }
}
