using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.GameImport.Aggregation.Aggregators.Augmenters.Quality;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.GameImport.Aggregation.Aggregators
{
    public class AggregateQuality : IAggregateLocalGame
    {
        public int Order => 1;

        private readonly List<IAugmentQuality> _augmentQualities;
        private readonly Logger _logger;

        public AggregateQuality(IEnumerable<IAugmentQuality> augmentQualities,
                                Logger logger)
        {
            _augmentQualities = augmentQualities.OrderBy(a => a.Order).ToList();
            _logger = logger;
        }

        public LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            Quality dumpTier = null;
            var dumpTierConfidence = Confidence.Default;
            var revision = new Revision(1);
            var revisionConfidence = Confidence.Default;
            var source = QualitySource.Unknown;
            var sourceConfidence = Confidence.Default;
            var resolution = 0;
            var resolutionConfidence = Confidence.Default;

            foreach (var augmentQuality in _augmentQualities)
            {
                var augmentedQuality = augmentQuality.AugmentQuality(localEpisode, downloadClientItem);
                if (augmentedQuality == null)
                {
                    continue;
                }

                _logger.Trace("Considering DumpTier {0} ({1}) Revision {2} from {3}", augmentedQuality.Quality, augmentedQuality.QualityConfidence, augmentedQuality.Revision, augmentQuality.Name);

                if (augmentedQuality.Quality != null && augmentedQuality.Quality != Quality.Unknown)
                {
                    if (augmentedQuality.QualityConfidence > dumpTierConfidence || dumpTier == null)
                    {
                        dumpTier = augmentedQuality.Quality;
                        dumpTierConfidence = augmentedQuality.QualityConfidence;
                    }
                }

                if (augmentedQuality.Revision != null)
                {
                    if (augmentedQuality.RevisionConfidence > revisionConfidence)
                    {
                        revision = augmentedQuality.Revision;
                        revisionConfidence = augmentedQuality.RevisionConfidence;
                    }
                    else if (augmentedQuality.RevisionConfidence == revisionConfidence &&
                             augmentedQuality.Revision > revision)
                    {
                        revision = augmentedQuality.Revision;
                        revisionConfidence = augmentedQuality.RevisionConfidence;
                    }
                }

                // Preserve legacy test source/resolution fallback if present
                if (source == QualitySource.Unknown ||
                    (augmentedQuality.SourceConfidence > sourceConfidence && augmentedQuality.Source != QualitySource.Unknown))
                {
                    source = augmentedQuality.Source;
                    sourceConfidence = augmentedQuality.SourceConfidence;
                }

                if (resolution == 0 ||
                    (augmentedQuality.ResolutionConfidence > resolutionConfidence && augmentedQuality.Resolution > 0))
                {
                    resolution = augmentedQuality.Resolution;
                    resolutionConfidence = augmentedQuality.ResolutionConfidence;
                }
            }

            // If cryptographic hash verification already succeeded, promote to VerifiedGoodDump
            if (localEpisode.RomHashVerified && (dumpTier == null || dumpTier == Quality.Unknown))
            {
                dumpTier = Quality.VerifiedGoodDump;
                dumpTierConfidence = Confidence.Tag;
            }

            Quality resolvedQuality;
            if (dumpTier != null && dumpTier != Quality.Unknown)
            {
                resolvedQuality = dumpTier;
            }
            else if (source != QualitySource.Unknown || resolution > 0)
            {
                resolvedQuality = QualityFinder.FindBySourceAndResolution(source, resolution);
            }
            else
            {
                resolvedQuality = Quality.Unknown;
            }

            var quality = new QualityModel(resolvedQuality, revision);

            if (resolution == 0)
            {
                quality.ResolutionDetectionSource = QualityDetectionSource.Unknown;
            }
            else if (resolutionConfidence == Confidence.MediaInfo)
            {
                quality.ResolutionDetectionSource = QualityDetectionSource.MediaInfo;
            }
            else if (resolutionConfidence == Confidence.Fallback)
            {
                quality.ResolutionDetectionSource = QualityDetectionSource.Extension;
            }
            else
            {
                quality.ResolutionDetectionSource = QualityDetectionSource.Name;
            }

            if (dumpTierConfidence != Confidence.Default)
            {
                quality.SourceDetectionSource = dumpTierConfidence == Confidence.Fallback ? QualityDetectionSource.Extension : QualityDetectionSource.Name;
            }
            else if (sourceConfidence == Confidence.Fallback)
            {
                quality.SourceDetectionSource = QualityDetectionSource.Extension;
            }
            else
            {
                quality.SourceDetectionSource = QualityDetectionSource.Name;
            }

            quality.RevisionDetectionSource = revisionConfidence == Confidence.Tag ? QualityDetectionSource.Name : QualityDetectionSource.Unknown;

            _logger.Debug("Using quality: {0}", quality);

            localEpisode.Quality = quality;

            return localEpisode;
        }
    }
}
