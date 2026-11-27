using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.LibraryImport
{
    [TestFixture]
    public class UnmanagedDiskScannerFixture : CoreTest<UnmanagedDiskScanner>
    {
        private string _tempRoot;

        [SetUp]
        public void Setup()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_uds_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);

            Mocker.GetMock<IPlatformCatalogService>()
                .Setup(s => s.GetAll())
                .Returns(new List<PlatformDefinition>());

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Mocker.GetMock<IDatCacheService>()
                .Setup(s => s.FindBestMatch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((DatEntry)null);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void should_not_bleed_parent_path_prefix_from_atari_jaguar_onto_jaguar_cd()
        {
            var jaguarPath = Path.Combine(_tempRoot, "Atari", "Atari Jaguar");
            var jaguarCdPath = Path.Combine(_tempRoot, "Atari", "Atari Jaguar CD");
            var jaguarFile = Path.Combine(jaguarPath, "USA", "Tempest 2000 (USA).j64");
            var jaguarCdFile = Path.Combine(jaguarCdPath, "USA", "Battlemorph (USA).chd");

            Directory.CreateDirectory(Path.GetDirectoryName(jaguarFile));
            Directory.CreateDirectory(Path.GetDirectoryName(jaguarCdFile));
            File.WriteAllBytes(jaguarFile, new byte[2048]);
            File.WriteAllBytes(jaguarCdFile, new byte[2048]);

            var jaguarSeries = new Series
            {
                Id = 1,
                Title = "Atari Jaguar",
                CleanTitle = "atarijaguar",
                TitleSlug = "jaguar",
                Path = jaguarPath
            };

            var jaguarCdSeries = new Series
            {
                Id = 2,
                Title = "Atari Jaguar CD",
                CleanTitle = "atarijaguarcd",
                TitleSlug = "jaguarcd",
                Path = jaguarCdPath
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { jaguarSeries, jaguarCdSeries });

            var scanned = Subject.Scan(jaguarCdPath);

            scanned.Should().ContainSingle();
            scanned[0].Series.Should().NotBeNull();
            scanned[0].Series.Id.Should().Be(2);
            scanned[0].Series.TitleSlug.Should().Be("jaguarcd");

            var parentScanned = Subject.Scan(jaguarPath);

            parentScanned.Should().ContainSingle();
            parentScanned[0].Series.Should().NotBeNull();
            parentScanned[0].Series.Id.Should().Be(1);
            parentScanned[0].Series.TitleSlug.Should().Be("jaguar");
            scanned[0].IsDatHashVerified.Should().BeFalse();
            scanned[0].IsHighConfidenceMatch.Should().BeFalse();
            scanned[0].Episodes.Should().BeEmpty();
        }

        [Test]
        public void should_not_bleed_parent_path_prefix_from_n64_onto_64dd()
        {
            var n64Path = Path.Combine(_tempRoot, "Nintendo", "Nintendo 64");
            var ddPath = Path.Combine(_tempRoot, "Nintendo", "Nintendo 64DD");
            var ddFile = Path.Combine(ddPath, "Japan", "F-Zero X Expansion Kit (Japan).ndd");

            Directory.CreateDirectory(Path.GetDirectoryName(ddFile));
            File.WriteAllBytes(ddFile, new byte[2048]);

            var n64Series = new Series
            {
                Id = 10,
                Title = "Nintendo 64",
                CleanTitle = "nintendo64",
                TitleSlug = "n64",
                Path = n64Path
            };

            var ddSeries = new Series
            {
                Id = 11,
                Title = "Nintendo 64DD",
                CleanTitle = "nintendo64dd",
                TitleSlug = "64dd",
                Path = ddPath
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { n64Series, ddSeries });

            var scanned = Subject.Scan(ddPath);

            scanned.Should().ContainSingle();
            scanned[0].Series.Should().NotBeNull();
            scanned[0].Series.Id.Should().Be(11);
            scanned[0].Series.TitleSlug.Should().Be("64dd");
        }

        [Test]
        public void should_not_substring_match_bios_platform_onto_addon_when_parent_only_matches()
        {
            var jaguarCdPath = Path.Combine(_tempRoot, "Atari Jaguar CD");
            var biosDir = Path.Combine(_tempRoot, "Unknown");
            var biosFilePath = Path.Combine(biosDir, "jaguar_boot.rom");

            Directory.CreateDirectory(biosDir);
            File.WriteAllBytes(biosFilePath, new byte[2048]);

            var biosEntry = new BiosFile
            {
                FileName = "jaguar_boot.rom",
                GamePlatform = "jaguar",
                Description = "Atari Jaguar Boot ROM"
            };

            Mocker.GetMock<IBiosService>()
                .Setup(b => b.IsKnownBiosFileName("jaguar_boot.rom"))
                .Returns(true);

            Mocker.GetMock<IBiosService>()
                .Setup(b => b.TryGetBiosFile("jaguar_boot.rom", out biosEntry))
                .Returns(true);

            var jaguarCdSeries = new Series
            {
                Id = 20,
                Title = "Atari Jaguar CD",
                CleanTitle = "atarijaguarcd",
                TitleSlug = "jaguarcd",
                Path = jaguarCdPath
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { jaguarCdSeries });

            var scanned = Subject.Scan(biosDir);

            scanned.Should().ContainSingle();
            scanned[0].Series.Should().BeNull();
        }

        private delegate bool ExtractChdTracksDelegate(string chdPath, string targetDirectory, out string cuePath);

        [Test]
        public void should_verify_tgcd_chd_by_extracting_and_matching_redump_data_track()
        {
            var tgcdDir = Path.Combine(_tempRoot, "NEC TurboGrafx-CD");
            var chdPath = Path.Combine(tgcdDir, "Lords of Thunder (USA).chd");
            Directory.CreateDirectory(tgcdDir);
            File.WriteAllBytes(chdPath, new byte[] { 0x4D, 0x43, 0x6F, 0x6D, 0x70, 0x48, 0x44, 0x00 });

            var track1Bytes = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            var track2Bytes = new byte[] { 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC };

            var tempSeedFile = Path.Combine(_tempRoot, "seed_track2.bin");
            File.WriteAllBytes(tempSeedFile, track2Bytes);
            var expectedTrack2Hashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(tempSeedFile);
            var expectedCrc32 = expectedTrack2Hashes["CRC32"];
            var expectedSha1 = expectedTrack2Hashes["SHA-1"];
            var expectedMd5 = expectedTrack2Hashes["MD5"];

            var datEntry = new DatEntry
            {
                GameName = "Lords of Thunder (USA)",
                RomName = "Lords of Thunder (USA) (Track 2).bin",
                Crc32 = expectedCrc32,
                Sha1 = expectedSha1,
                Md5 = expectedMd5
            };

            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindBestMatch(expectedCrc32, expectedMd5, expectedSha1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(datEntry);

            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindPlatformByHash(expectedCrc32, expectedSha1, expectedMd5, It.IsAny<string>()))
                .Returns(new DatEntryWithPlatform
                {
                    DatEntry = datEntry,
                    PlatformSlug = "tgcd"
                });

            string capturedExtractDir = null;
            Mocker.GetMock<IDiscCompressionService>()
                .Setup(c => c.ExtractChdTracksForVerification(chdPath, It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(new ExtractChdTracksDelegate((string src, string targetDir, out string cueOut) =>
                {
                    capturedExtractDir = targetDir;
                    Directory.CreateDirectory(targetDir);
                    File.WriteAllBytes(Path.Combine(targetDir, "disc (Track 1).bin"), track1Bytes);
                    File.WriteAllBytes(Path.Combine(targetDir, "disc (Track 2).bin"), track2Bytes);
                    cueOut = Path.Combine(targetDir, "disc.cue");
                    File.WriteAllText(cueOut, string.Join(
                        "\n",
                        "FILE \"disc (Track 1).bin\" BINARY",
                        "  TRACK 01 AUDIO",
                        "    INDEX 01 00:00:00",
                        "FILE \"disc (Track 2).bin\" BINARY",
                        "  TRACK 02 MODE1/2352",
                        "    INDEX 01 00:02:00"));
                    return true;
                }));

            var tgcdSeries = new Series
            {
                Id = 30,
                TvdbId = 1021,
                Title = "NEC TurboGrafx-CD",
                CleanTitle = "necturbografxcd",
                TitleSlug = "tgcd",
                Path = tgcdDir
            };

            var gameEpisode = new Episode
            {
                Id = 301,
                SeriesId = 30,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Lords of Thunder",
                FinaleType = "Retail (v1.0)"
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { tgcdSeries });

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(30))
                .Returns(new List<Episode> { gameEpisode });

            var scanned = Subject.Scan(tgcdDir);

            scanned.Should().ContainSingle();
            scanned[0].Series.Should().NotBeNull();
            scanned[0].Series.TitleSlug.Should().Be("tgcd");
            scanned[0].Episodes.Should().ContainSingle(e => e.Id == 301);
            scanned[0].IsHighConfidenceMatch.Should().BeTrue();
            scanned[0].Hashes["CRC32"].Should().Be(expectedCrc32);
            scanned[0].Hashes["ROM_NAME"].Should().Be("Lords of Thunder (USA).chd");
            capturedExtractDir.Should().NotBeNullOrWhiteSpace();
            Directory.Exists(capturedExtractDir).Should().BeFalse();
        }

        [Test]
        public void should_not_lock_chd_file_in_tg16_folder_to_cartridge_platform()
        {
            var importDir = Path.Combine(_tempRoot, "Downloads", "TG16");
            var chdPath = Path.Combine(importDir, "Rondo of Blood (Japan).chd");
            Directory.CreateDirectory(importDir);
            File.WriteAllBytes(chdPath, new byte[1024]);

            var tg16Series = new Series
            {
                Id = 40,
                Title = "NEC TurboGrafx-16",
                CleanTitle = "necturbografx16",
                TitleSlug = "tg16",
                Path = Path.Combine(_tempRoot, "Library", "NEC TurboGrafx-16")
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { tg16Series });

            var scanned = Subject.Scan(importDir);

            scanned.Should().ContainSingle();
            scanned[0].Series.Should().BeNull();
            scanned[0].IsHighConfidenceMatch.Should().BeFalse();
        }

        [Test]
        public void should_not_create_fake_episode_or_mark_verified_when_download_iso_has_no_dat_match()
        {
            var downloadDir = Path.Combine(_tempRoot, "Downloads");
            var isoPath = Path.Combine(downloadDir, "ROBINSONS_REQUEIM.iso");
            Directory.CreateDirectory(downloadDir);
            File.WriteAllBytes(isoPath, new byte[4096]);

            var jaguarCdSeries = new Series
            {
                Id = 50,
                TvdbId = 1050,
                Title = "Atari Jaguar CD",
                CleanTitle = "atarijaguarcd",
                TitleSlug = "jaguarcd",
                Path = Path.Combine(_tempRoot, "Library", "Atari Jaguar CD")
            };

            var realEpisode = new Episode
            {
                Id = 501,
                SeriesId = 50,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Robinson's Requiem"
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { jaguarCdSeries });

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(50))
                .Returns(new List<Episode> { realEpisode });

            var localEpisode = new LocalEpisode
            {
                Path = isoPath,
                Series = jaguarCdSeries,
                Episodes = new List<Episode>()
            };

            Subject.VerifyAndEnrichLocalEpisode(localEpisode);

            localEpisode.Episodes.Should().BeEmpty();
            localEpisode.RomHashVerified.Should().BeFalse();
            Mocker.GetMock<IEpisodeService>()
                .Verify(e => e.InsertMany(It.IsAny<List<Episode>>()), Times.Never());
        }

        [Test]
        public void should_mark_rom_hash_verified_true_when_dat_hash_matches()
        {
            var downloadDir = Path.Combine(_tempRoot, "Downloads");
            var romPath = Path.Combine(downloadDir, "ROBINSONS_REQUEIM.iso");
            Directory.CreateDirectory(downloadDir);
            var bytes = new byte[] { 10, 20, 30, 40, 50 };
            File.WriteAllBytes(romPath, bytes);

            var expectedHashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(romPath);
            var expectedCrc32 = expectedHashes["CRC32"];
            var expectedMd5 = expectedHashes["MD5"];
            var expectedSha1 = expectedHashes["SHA-1"];

            var jaguarCdSeries = new Series
            {
                Id = 50,
                TvdbId = 1050,
                Title = "Atari Jaguar CD",
                CleanTitle = "atarijaguarcd",
                TitleSlug = "jaguarcd",
                Path = Path.Combine(_tempRoot, "Library", "Atari Jaguar CD")
            };

            var realEpisode = new Episode
            {
                Id = 501,
                SeriesId = 50,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Robinson's Requiem",
                FinaleType = "Retail (v1.0)"
            };

            var datEntry = new DatEntry
            {
                GameName = "Robinson's Requiem (USA)",
                RomName = "Robinson's Requiem (USA).iso",
                Crc32 = expectedCrc32,
                Md5 = expectedMd5,
                Sha1 = expectedSha1
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { jaguarCdSeries });

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(50))
                .Returns(new List<Episode> { realEpisode });

            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindBestMatch(expectedCrc32, expectedMd5, expectedSha1, It.IsAny<string>(), "jaguarcd", "Atari Jaguar CD"))
                .Returns(datEntry);

            var localEpisode = new LocalEpisode
            {
                Path = romPath,
                Series = jaguarCdSeries,
                Episodes = new List<Episode>()
            };

            Subject.VerifyAndEnrichLocalEpisode(localEpisode);

            localEpisode.RomHashVerified.Should().BeTrue();
            localEpisode.Episodes.Should().ContainSingle(e => e.Id == 501);
            Mocker.GetMock<IEpisodeService>()
                .Verify(e => e.InsertMany(It.IsAny<List<Episode>>()), Times.Never());
        }
        [Test]
        public void should_not_auto_match_episode_by_filename_when_dat_hash_is_missing()
        {
            var nesPath = Path.Combine(_tempRoot, "Nintendo", "NES");
            var romPath = Path.Combine(nesPath, "USA", "Super Mario Bros. (USA).nes");
            Directory.CreateDirectory(Path.GetDirectoryName(romPath));
            File.WriteAllBytes(romPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

            var nesSeries = new Series
            {
                Id = 55,
                Title = "Nintendo Entertainment System",
                CleanTitle = "nes",
                TitleSlug = "nes",
                Path = nesPath
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { nesSeries });

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(55))
                .Returns(new List<Episode>
                {
                    new Episode { Id = 5501, SeriesId = 55, SeasonNumber = 1, Title = "Super Mario Bros.", FinaleType = "Retail (v1.0)" }
                });

            var scanned = Subject.Scan(nesPath);

            scanned.Should().ContainSingle();
            scanned[0].IsDatHashVerified.Should().BeFalse();
            scanned[0].IsHighConfidenceMatch.Should().BeFalse();
            scanned[0].Episodes.Should().BeEmpty();
        }
    }
}
