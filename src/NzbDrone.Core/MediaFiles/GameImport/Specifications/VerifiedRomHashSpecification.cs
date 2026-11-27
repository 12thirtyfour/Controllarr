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
            if (localEpisode == null || _datCacheService == null)
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
                if (IsValidDatEntry(matchedDat) ||
                    (sha256 != null && IsValidDatEntry(_datCacheService.FindBySha256(sha256))) ||
                    (sha1 != null && IsValidDatEntry(_datCacheService.FindBySha1(sha1))) ||
                    (md5 != null && IsValidDatEntry(_datCacheService.FindByMd5(md5))) ||
                    (crc32 != null && IsValidDatEntry(_datCacheService.FindByCrc32(crc32))))
                {
                    localEpisode.RomHashVerified = true;
                    return ImportSpecDecision.Accept();
                }
            }

            _logger.Warn("Rejecting ROM with unverified DAT hash: {0}", localEpisode.Path);
            return ImportSpecDecision.Reject(
                ImportRejectionReason.UnverifiedRomHash,
                "Unverified ROM Hash: Checksum does not match any verified No-Intro, Redump, TOSEC, or SmokeMonster SMDB DAT entry.");
        }

        private static bool IsValidDatEntry(DatEntry entry)
        {
            return entry != null &&
                   (entry.Id > 0 ||
                    !string.IsNullOrWhiteSpace(entry.GameName) ||
                    !string.IsNullOrWhiteSpace(entry.RomName) ||
                    !string.IsNullOrWhiteSpace(entry.Crc32) ||
                    !string.IsNullOrWhiteSpace(entry.Sha1) ||
                    !string.IsNullOrWhiteSpace(entry.Md5) ||
                    !string.IsNullOrWhiteSpace(entry.Sha256));
        }
    }
}
