using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.LibraryImport;
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
    }
}
