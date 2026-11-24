using NLog;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class VerifiedRomHashSpecification : IImportDecisionEngineSpecification
    {
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;

        public VerifiedRomHashSpecification(IDatCacheService datCacheService, Logger logger)
        {
            _datCacheService = datCacheService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (downloadClientItem == null || localEpisode == null || _datCacheService == null)
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.RomHashVerified)
            {
                return ImportSpecDecision.Accept();
            }

            var hasComputedRomHashes = localEpisode.RomHashes != null && localEpisode.RomHashes.Count > 0;
            if (hasComputedRomHashes)
            {
                localEpisode.RomHashes.TryGetValue("SHA-256", out var sha256);
                localEpisode.RomHashes.TryGetValue("SHA-1", out var sha1);
                localEpisode.RomHashes.TryGetValue("MD5", out var md5);
                localEpisode.RomHashes.TryGetValue("CRC32", out var crc32);

                var matchedDat = _datCacheService.FindBestMatch(crc32, md5, sha1, sha256, localEpisode.Series?.TitleSlug, localEpisode.Series?.Title);
                if (matchedDat != null ||
                    (sha256 != null && _datCacheService.FindBySha256(sha256) != null) ||
                    (sha1 != null && _datCacheService.FindBySha1(sha1) != null) ||
                    (md5 != null && _datCacheService.FindByMd5(md5) != null) ||
                    (crc32 != null && _datCacheService.FindByCrc32(crc32) != null))
                {
                    localEpisode.RomHashVerified = true;
                    return ImportSpecDecision.Accept();
                }
            }

            var hasPlatformEntries = localEpisode.Series != null &&
                _datCacheService.HasEntriesForPlatform(localEpisode.Series.TitleSlug, localEpisode.Series.Title);

            if (hasComputedRomHashes || hasPlatformEntries)
            {
                _logger.Warn("Holding unverified ROM download in Activity Queue for manual review: {0}", localEpisode.Path);
                return ImportSpecDecision.Reject(
                    ImportRejectionReason.UnverifiedRomHash,
                    "Unverified ROM Hash: Checksum does not match any verified No-Intro, Redump, TOSEC, or SmokeMonster SMDB entry. Inspect and approve manually if trusted.");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
