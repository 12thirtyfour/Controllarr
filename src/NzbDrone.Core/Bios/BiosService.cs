using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NLog;

namespace NzbDrone.Core.Bios
{
    public interface IBiosService
    {
        bool VerifyBios(string filePath, string expectedMd5, string expectedSha1);
        List<BiosFile> GetCatalog(string platformSlug = null);
        List<BiosFile> ScanAndVerifyBiosDirectory(string biosDirectory, string platformSlug = null);
    }

    public class BiosService : IBiosService
    {
        private static readonly List<BiosFile> CanonicalBiosCatalog = new List<BiosFile>
        {
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5500.bin",
                ExpectedMd5 = "8dd7d5296a650fac7319bce665a6a53c",
                ExpectedSha1 = "b05def971d8ec59f346f2d9ac21fb742e3eb6917",
                Description = "PlayStation BIOS (JP v3.0)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5501.bin",
                ExpectedMd5 = "490f666e1afb15b7362b406ed1cea246",
                ExpectedSha1 = "0555c6fae8906f3f09baf5988f00e55f88e9f30b",
                Description = "PlayStation BIOS (US v3.0)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5502.bin",
                ExpectedMd5 = "32736f17079d0b2b7024407c39bd3050",
                ExpectedSha1 = "d786f0b9a3c51c42f1dd61dc6fd7ca97fa39c932",
                Description = "PlayStation BIOS (EU v3.0)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_U.bin",
                ExpectedMd5 = "2efd74e3232ff260e371b99f84024f7f",
                ExpectedSha1 = "f4f315adcef9b8feb0364c21ab7f0eaf5457f3ed",
                Description = "Sega CD BIOS (US v1.10)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_E.bin",
                ExpectedMd5 = "e66fa1dc5820d254611fdcdba0662372",
                ExpectedSha1 = "f891e0ea651e2232af0c5c4cb46a0cae2ee8f356",
                Description = "Mega-CD BIOS (EU v1.00)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_J.bin",
                ExpectedMd5 = "278a9397d192149e84e820ac621a8edd",
                ExpectedSha1 = "4be18ff49c487c03219f029f1d0f08b9009b91be",
                Description = "Mega-CD BIOS (JP v1.00)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "sega_101.bin",
                ExpectedMd5 = "85ec9ca47d8f6807718151cbcca8b964",
                ExpectedSha1 = "2b710283400a2ab390d9818f4040a16b3d8eb501",
                Description = "Sega Saturn BIOS (JP v1.01)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "mpr-17933.bin",
                ExpectedMd5 = "3240872c70984b6cbfda1586cab68dbe",
                ExpectedSha1 = "96e106f740ab448cf89f0dd49dfbac7fe5391cb6",
                Description = "Sega Saturn BIOS (US/EU v1.00a)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dreamcast",
                FileName = "dc_boot.bin",
                ExpectedMd5 = "e10c53c2f8b90bab96ead2d368858623",
                ExpectedSha1 = "c5c61f0d97c74a2fd88052329a1e3e37c0ef0c77",
                Description = "Dreamcast Boot ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "gba",
                FileName = "gba_bios.bin",
                ExpectedMd5 = "a860e8c0b6d573d191e4ec7db1b1e4f6",
                ExpectedSha1 = "300c20df6731a33952ded8c436f7f186d25d3492",
                Description = "Game Boy Advance System ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "fds",
                FileName = "disksys.rom",
                ExpectedMd5 = "ca30b50f880eb660a320674ed365ef7a",
                ExpectedSha1 = "57fe1bdee955bb48d357e463ccbf129496930b62",
                Description = "Famicom Disk System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "tgcd",
                FileName = "syscard3.pce",
                ExpectedMd5 = "38179df8f4ac870017db21ebcbf53114",
                ExpectedSha1 = "79f5ff55dd10f87c7fd7b8f40b3ff42f8d72c2a9",
                Description = "PC Engine Super CD-ROM2 System v3.0",
                IsRequired = true
            }
        };

        private readonly Logger _logger;

        public BiosService(Logger logger)
        {
            _logger = logger;
        }

        public List<BiosFile> GetCatalog(string platformSlug = null)
        {
            var query = CanonicalBiosCatalog.Select(CloneBiosFile);

            if (!string.IsNullOrWhiteSpace(platformSlug))
            {
                query = query.Where(b => string.Equals(b.GamePlatform, platformSlug.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        public List<BiosFile> ScanAndVerifyBiosDirectory(string biosDirectory, string platformSlug = null)
        {
            var catalog = GetCatalog(platformSlug);

            if (string.IsNullOrWhiteSpace(biosDirectory) || !Directory.Exists(biosDirectory))
            {
                return catalog;
            }

            var existingFiles = Directory.GetFiles(biosDirectory, "*.*", SearchOption.AllDirectories);

            foreach (var entry in catalog)
            {
                var matchingFile = existingFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), entry.FileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                {
                    entry.IsVerified = VerifyBios(matchingFile, entry.ExpectedMd5, entry.ExpectedSha1);
                }
            }

            return catalog;
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

                    stream.Position = 0;

                    var sha1HashBytes = sha1.ComputeHash(stream);
                    var sha1Hash = BitConverter.ToString(sha1HashBytes).Replace("-", "").ToLowerInvariant();

                    var md5Match = string.IsNullOrWhiteSpace(expectedMd5) ||
                                   string.Equals(md5Hash, expectedMd5, StringComparison.InvariantCultureIgnoreCase);
                    var sha1Match = string.IsNullOrWhiteSpace(expectedSha1) ||
                                    string.Equals(sha1Hash, expectedSha1, StringComparison.InvariantCultureIgnoreCase);

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

        private static BiosFile CloneBiosFile(BiosFile source)
        {
            return new BiosFile
            {
                GamePlatform = source.GamePlatform,
                FileName = source.FileName,
                ExpectedMd5 = source.ExpectedMd5,
                ExpectedSha1 = source.ExpectedSha1,
                Description = source.Description,
                IsRequired = source.IsRequired,
                IsVerified = source.IsVerified
            };
        }
    }
}
