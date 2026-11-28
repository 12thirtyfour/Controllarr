using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.GameImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class VerifiedRomHashSpecificationFixture : CoreTest<VerifiedRomHashSpecification>
    {
        [Test]
        public void should_accept_when_rom_hash_is_verified()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Super Mario World (USA).sfc",
                RomHashVerified = true,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "b19ed489" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-1",
                Title = "Super Mario World (USA)"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_hold_in_activity_queue_when_automated_download_hash_is_unverified()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Unknown Dump (USA).sfc",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "deadbeef" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-2",
                Title = "Unknown Dump (USA)"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
            decision.Message.Should().NotContain("Inspect and approve manually");
        }

        [Test]
        public void should_reject_unverified_rom_when_download_client_item_is_null()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\ManualImport\Custom Translation.sfc",
                RomHashVerified = false
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, null);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_reject_unhashed_rom_even_when_platform_dat_cache_is_empty()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Mystery Game.rom",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>()
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-empty" });
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_accept_disk_or_manual_import_when_rom_hash_matches_dat_cache()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindBestMatch("b19ed489", null, null, null, null, null))
                .Returns(new DatEntry
                {
                    Id = 2,
                    GameName = "Super Mario World (USA)",
                    RomName = "Super Mario World (USA).sfc",
                    Crc32 = "b19ed489"
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\ManualImport\Super Mario World (USA).sfc",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "b19ed489" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, null);
            decision.Accepted.Should().BeTrue();
            localEpisode.RomHashVerified.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_only_sfv_matches_without_dat_database_entry()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Pokemon Y\Pokemon Y.3ds",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "8b490e08" },
                    { "SFV_CRC32", "8b490e08" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-3",
                Title = "Pokemon Y"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
            localEpisode.RomHashVerified.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_rom_hash_matches_unified_dat_cache()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindBestMatch("b19ed489", "cdb36225dd1e7c4f22cde6924dd79669", "6b47bb75d16514b6a476aa0c73a683a2a4c18765", null, null, null))
                .Returns(new DatEntry
                {
                    Id = 1,
                    GameName = "Super Mario World (USA)",
                    RomName = "Super Mario World (USA).sfc",
                    Crc32 = "b19ed489"
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Super Mario World (USA).sfc",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "b19ed489" },
                    { "MD5", "cdb36225dd1e7c4f22cde6924dd79669" },
                    { "SHA-1", "6b47bb75d16514b6a476aa0c73a683a2a4c18765" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-4",
                Title = "Super Mario World (USA)"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeTrue();
            localEpisode.RomHashVerified.Should().BeTrue();
        }

        [Test]
        public void should_accept_chd_under_missing_format_hash_exception_when_platform_has_iso_hashes_only()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("psx", "Sony PlayStation"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 10,
                        GameName = "Crash Bandicoot (USA)",
                        RomName = "Crash Bandicoot (USA).iso",
                        Crc32 = "aabbccdd",
                        Sha1 = "0123456789abcdef0123456789abcdef01234567"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Crash Bandicoot (USA).chd",
                Series = new NzbDrone.Core.Tv.Series { Id = 1, TitleSlug = "psx", Title = "Sony PlayStation" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 1, Title = "Crash Bandicoot" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "11223344" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-chd" });
            decision.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_chd_when_inner_tracks_were_hashed_and_did_not_match_dat()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("psx", "Sony PlayStation"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 10,
                        GameName = "Crash Bandicoot (USA)",
                        RomName = "Crash Bandicoot (USA).bin",
                        Crc32 = "aabbccdd"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Crash Bandicoot (USA) (Bad Dump).chd",
                Series = new NzbDrone.Core.Tv.Series { Id = 1, TitleSlug = "psx", Title = "Sony PlayStation" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 1, Title = "Crash Bandicoot" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "deadbeef" },
                    { "INNER_TRACKS_HASHED", "true" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-bad-chd" });
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_reject_chd_when_platform_dat_cache_has_no_hashed_entries()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("psx", "Sony PlayStation"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 11,
                        GameName = "Hashless Entry",
                        RomName = "Hashless Entry.bin"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Crash Bandicoot (USA).chd",
                Series = new NzbDrone.Core.Tv.Series { Id = 1, TitleSlug = "psx", Title = "Sony PlayStation" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 1, Title = "Crash Bandicoot" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "11223344" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-hashless-platform" });
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_reject_chd_when_platform_dat_cache_already_has_chd_hashes()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("psx", "Sony PlayStation"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 12,
                        GameName = "Crash Bandicoot (USA)",
                        RomName = "Crash Bandicoot (USA).chd",
                        Crc32 = "aabbccdd"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Crash Bandicoot (USA).chd",
                Series = new NzbDrone.Core.Tv.Series { Id = 1, TitleSlug = "psx", Title = "Sony PlayStation" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 1, Title = "Crash Bandicoot" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "11223344" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-has-chd" });
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_reject_iso_when_hash_does_not_match_platform_iso_dat_hashes()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("ps2", "Sony PlayStation 2"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 13,
                        GameName = "Shadow of the Colossus (USA)",
                        RomName = "Shadow of the Colossus (USA).iso",
                        Crc32 = "aabbccdd"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Shadow of the Colossus (USA).iso",
                Series = new NzbDrone.Core.Tv.Series { Id = 2, TitleSlug = "ps2", Title = "Sony PlayStation 2" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 2, Title = "Shadow of the Colossus" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "deadbeef" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, new DownloadClientItem { DownloadId = "dl-bad-iso" });
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_accept_zipped_gameandwatch_gnw_rom_under_missing_format_hash_exception_when_dat_only_has_mgw_hashes()
        {
            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform("gameandwatch", "Nintendo Game & Watch"))
                .Returns(new List<DatEntry>
                {
                    new DatEntry
                    {
                        Id = 99,
                        GameName = "Oil Panic (Multi Screen)",
                        RomName = "Oil Panic (Multi Screen).mgw",
                        Crc32 = "31c288c9",
                        Sha1 = "4bfd0fba94a9927cefc925db8196b063c5dd9b19"
                    }
                });

            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Roms\Nintendo\Game & Watch\USA\Oil Panic.zip",
                Series = new NzbDrone.Core.Tv.Series { Id = 179, TitleSlug = "gameandwatch", Title = "Nintendo Game & Watch" },
                Episodes = new List<NzbDrone.Core.Tv.Episode> { new NzbDrone.Core.Tv.Episode { Id = 1, Title = "Oil Panic" } },
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "deadbeef" },
                    { "ROM_NAME", "Oil Panic.gnw" }
                }
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, null);
            decision.Accepted.Should().BeTrue();
        }
    }
}
