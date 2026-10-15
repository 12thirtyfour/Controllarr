using System;
using System.IO;
using System.Security.Cryptography;
using NLog;

namespace NzbDrone.Core.Bios
{
    public interface IBiosService
    {
        bool VerifyBios(string filePath, string expectedMd5, string expectedSha1);
    }

    public class BiosService : IBiosService
    {
        private readonly Logger _logger;

        public BiosService(Logger logger)
        {
            _logger = logger;
        }

        public bool VerifyBios(string filePath, string expectedMd5, string expectedSha1)
        {
            if (!File.Exists(filePath))
            {
                _logger.Warn($"BIOS file not found at path: {filePath}");
                return false;
            }

            try
            {
                using (var md5 = MD5.Create())
                using (var sha1 = SHA1.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var md5HashBytes = md5.ComputeHash(stream);
                    var md5Hash = BitConverter.ToString(md5HashBytes).Replace("-", "").ToLowerInvariant();

                    stream.Position = 0; // Reset stream

                    var sha1HashBytes = sha1.ComputeHash(stream);
                    var sha1Hash = BitConverter.ToString(sha1HashBytes).Replace("-", "").ToLowerInvariant();

                    var md5Match = string.Equals(md5Hash, expectedMd5, StringComparison.InvariantCultureIgnoreCase);
                    var sha1Match = string.Equals(sha1Hash, expectedSha1, StringComparison.InvariantCultureIgnoreCase);

                    if (md5Match && sha1Match)
                    {
                        _logger.Debug($"BIOS Verified successfully: {filePath}");
                        return true;
                    }

                    _logger.Warn($"BIOS Verification failed. MD5: {md5Hash} (Expected: {expectedMd5}), SHA1: {sha1Hash} (Expected: {expectedSha1})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error verifying BIOS file: {filePath}");
                return false;
            }
        }
    }
}
