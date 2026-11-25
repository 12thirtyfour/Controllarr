using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MetadataSource.Game;

namespace NzbDrone.Core.Test.DatTests
{
    [TestFixture]
    public class PlatformVerificationInfoFixture
    {
        [Test]
        public void should_return_dynamic_verification_info_for_atari_2600()
        {
            var info = GameCatalogProxy.GetPlatformVerificationInfo(1034, "atari2600", "Atari 2600");

            info.Databases.Should().Equal("No-Intro", "SMDB");
            info.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Archive Hashing");
        }

        [Test]
        public void should_return_dynamic_verification_info_for_atari_5200()
        {
            var info = GameCatalogProxy.GetPlatformVerificationInfo(1042, "atari5200", "Atari 5200");

            info.Databases.Should().Equal("No-Intro");
            info.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Archive Hashing");
        }

        [Test]
        public void should_return_dynamic_verification_info_for_optical_platforms()
        {
            var ps1 = GameCatalogProxy.GetPlatformVerificationInfo(1022, "ps1", "Sony PlayStation");
            ps1.Databases.Should().Equal("Redump");
            ps1.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Disc Track Hashing", "CHD Verify");

            var segacd = GameCatalogProxy.GetPlatformVerificationInfo(1016, "segacd", "Sega CD");
            segacd.Databases.Should().Equal("Redump", "SMDB");
            segacd.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Disc Track Hashing", "CHD Verify");

            var ps2 = GameCatalogProxy.GetPlatformVerificationInfo(1023, "ps2", "Sony PlayStation 2");
            ps2.Databases.Should().Equal("Redump");
            ps2.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "CHD Verify");

            var gamecube = GameCatalogProxy.GetPlatformVerificationInfo(1007, "gamecube", "Nintendo GameCube");
            gamecube.Databases.Should().Equal("Redump", "GameTDB");
            gamecube.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "RVZ / Disc Hashing");

            var xbox360 = GameCatalogProxy.GetPlatformVerificationInfo(1033, "xbox360", "Microsoft Xbox 360");
            xbox360.Databases.Should().Equal("Redump");
            xbox360.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Disc Image Hashing");
        }

        [Test]
        public void should_return_dynamic_verification_info_for_switch_and_neogeo()
        {
            var nsw = GameCatalogProxy.GetPlatformVerificationInfo(1014, "switch", "Nintendo Switch");
            nsw.Databases.Should().Equal("NSWDB", "GameTDB");
            nsw.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256");

            var neogeo = GameCatalogProxy.GetPlatformVerificationInfo(1038, "neogeo", "SNK Neo Geo");
            neogeo.Databases.Should().Equal("FBNeo", "No-Intro", "SMDB");
            neogeo.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Archive Hashing");
        }

        [TestCase(EncryptionPreference.NotEncrypted, "No-Intro (Decrypted)")]
        [TestCase(EncryptionPreference.Encrypted, "No-Intro (Encrypted)")]
        [TestCase(EncryptionPreference.Both, "No-Intro (Decrypted & Encrypted)")]
        public void should_reflect_nds_encryption_preference(EncryptionPreference preference, string expectedNoIntro)
        {
            var info = GameCatalogProxy.GetPlatformVerificationInfo(1012, "nds", "Nintendo DS", preference);

            info.Databases.Should().Equal(expectedNoIntro, "GameTDB");
            info.Methods.Should().Equal("CRC32", "MD5", "SHA-1", "SHA-256", "Archive Hashing");
        }

        [Test]
        public void should_use_live_cached_dat_files_and_ignore_supplement_or_other_platform_dats()
        {
            var cachedDats = new List<DatFile>
            {
                new DatFile
                {
                    Id = 1,
                    Name = "Atari - 2600 (No-Intro)",
                    Description = "atari2600",
                    Category = "No-Intro"
                },
                new DatFile
                {
                    Id = 2,
                    Name = "Atari - 2600 (Homebrew & Aftermarket)",
                    Description = "atari2600",
                    Category = "Homebrew & Aftermarket"
                },
                new DatFile
                {
                    Id = 3,
                    Name = "Atari - 2600 (ROM Hacks & Translations)",
                    Description = "atari2600",
                    Category = "ROM Hacks & Translations"
                },
                new DatFile
                {
                    Id = 4,
                    Name = "My Custom Atari 2600 Verification DAT",
                    Description = "atari2600",
                    Category = "Custom"
                },
                new DatFile
                {
                    Id = 5,
                    Name = "Sony - PlayStation 2 (Redump)",
                    Description = "ps2",
                    Category = "Redump"
                },
                new DatFile
                {
                    Id = 6,
                    Name = "Sony - PlayStation (Redump)",
                    Description = "ps1",
                    Category = "Redump"
                }
            };

            var atariInfo = GameCatalogProxy.GetPlatformVerificationInfo(
                1034,
                "atari2600",
                "Atari 2600",
                EncryptionPreference.NotEncrypted,
                cachedDats);

            atariInfo.Databases.Should().Equal("No-Intro", "Custom DAT");

            var ps1Info = GameCatalogProxy.GetPlatformVerificationInfo(
                1022,
                "ps1",
                "Sony PlayStation",
                EncryptionPreference.NotEncrypted,
                cachedDats);

            ps1Info.Databases.Should().Equal("Redump");

            var ps1Matched = DatCacheService.FilterDatFilesForPlatform(cachedDats, "ps1", "Sony PlayStation", "Sony - PlayStation");
            ps1Matched.Should().ContainSingle(d => d.Id == 6);
        }

        [Test]
        public void should_collapse_nds_decrypted_and_encrypted_cached_dats()
        {
            var cachedDats = new List<DatFile>
            {
                new DatFile
                {
                    Id = 10,
                    Name = "Nintendo - Nintendo DS (No-Intro (Decrypted))",
                    Description = "nds",
                    Category = "No-Intro (Decrypted)"
                },
                new DatFile
                {
                    Id = 11,
                    Name = "Nintendo - Nintendo DS (No-Intro (Encrypted))",
                    Description = "nds",
                    Category = "No-Intro (Encrypted)"
                },
                new DatFile
                {
                    Id = 12,
                    Name = "Nintendo DS (GameTDB)",
                    Description = "nds",
                    Category = "GameTDB"
                }
            };

            var info = GameCatalogProxy.GetPlatformVerificationInfo(
                1012,
                "nds",
                "Nintendo DS",
                EncryptionPreference.Both,
                cachedDats);

            info.Databases.Should().Equal("No-Intro (Decrypted & Encrypted)", "GameTDB");
        }
    }
}
