using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryImportTests
{
    [TestFixture]
    public class UnmanagedDiskScannerFixture : CoreTest<UnmanagedDiskScanner>
    {
        private Mock<ISeriesService> _seriesService;
        private Mock<IEpisodeService> _episodeService;
        private string _tempPath;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPlatformCatalogService>().Setup(c => c.GetAll()).Returns(new List<PlatformDefinition>
            {
                new PlatformDefinition { RomFileExtensions = new List<string> { ".smc", ".sfc", ".nes" } }
            });
            _seriesService = Mocker.GetMock<ISeriesService>();
            _episodeService = Mocker.GetMock<IEpisodeService>();

            var datCache = Mocker.GetMock<IDatCacheService>();
            datCache.Setup(c => c.FindByCrc32(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindByMd5(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindBySha1(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindBySha256(It.IsAny<string>())).Returns(new DatEntry());

            _tempPath = Path.Combine(Path.GetTempPath(), "UnmanagedDiskScannerTests");
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }

            Directory.CreateDirectory(_tempPath);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        [Test]
        public void Scan_FlatDirectory_ShouldReturnItems()
        {
            // Arrange
            var romPath = Path.Combine(_tempPath, "Super Mario World (USA).sfc");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "SNES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 1, Title = "Super Mario World", SeriesId = 1 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Episodes.First().Title.Should().Be("Super Mario World");
            results.First().Series.Title.Should().Be("SNES");
            results.First().IsHighConfidenceMatch.Should().BeTrue();
            results.First().Hashes.Should().ContainKey("SHA-256");
            results.First().Hashes.Should().ContainKey("SHA-1");
            results.First().Hashes.Should().ContainKey("MD5");
            results.First().Hashes.Should().ContainKey("CRC32");
        }

        [Test]
        public void Scan_BucketedDirectory_ShouldReturnItems()
        {
            // Arrange
            var bucketDir = Path.Combine(_tempPath, "A-F");
            Directory.CreateDirectory(bucketDir);
            var romPath = Path.Combine(bucketDir, "Chrono Trigger (USA).sfc");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "SNES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 2, Title = "Chrono Trigger", SeriesId = 1 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Episodes.First().Title.Should().Be("Chrono Trigger");
        }

        [Test]
        public void Inspect_ZipHashes_WithoutExtraction()
        {
            // Arrange
            var zipPath = Path.Combine(_tempPath, "Game.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Game.nes");
                using (var stream = entry.Open())
                {
                    stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 2, Title = "NES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(2)).Returns(new List<Episode>
            {
                new Episode { Id = 3, Title = "Game", SeriesId = 2 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Hashes.Should().ContainKey("CRC32");
            results.First().Hashes.Should().ContainKey("SHA-1");
            results.First().Hashes.Should().ContainKey("SHA-256");
        }

        [Test]
        public void Scan_ShouldResolveGameFromUnifiedDatHashWhenFilenameIsObscured()
        {
            var romPath = Path.Combine(_tempPath, "unlabeled_dump_01.sfc");
            File.WriteAllBytes(romPath, new byte[] { 9, 8, 7 });

            Mocker.GetMock<IDatCacheService>()
                .Setup(c => c.FindBySha256(It.IsAny<string>()))
                .Returns(new DatEntry
                {
                    GameName = "EarthBound (USA)",
                    RomName = "EarthBound (USA).sfc"
                });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "Super Nintendo Entertainment System" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 42, Title = "EarthBound", SeriesId = 1, SeasonNumber = 1 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Series.Title.Should().Be("Super Nintendo Entertainment System");
            results.First().Episodes.First().Title.Should().Be("EarthBound");
            results.First().SeasonNumber.Should().Be(1);
            results.First().IsHighConfidenceMatch.Should().BeTrue();
        }

        [Test]
        public void Scan_MultiRomCollectionArchive_ShouldReturnIndividualItemsForEachRom()
        {
            var zipPath = Path.Combine(_tempPath, "@NES2.0 2023-12-16.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry1 = archive.CreateEntry("Super Mario Bros (USA).nes");
                using (var s1 = entry1.Open())
                {
                    s1.Write(new byte[] { 10, 20, 30 }, 0, 3);
                }

                var entry2 = archive.CreateEntry("The Legend of Zelda (USA).nes");
                using (var s2 = entry2.Open())
                {
                    s2.Write(new byte[] { 40, 50, 60 }, 0, 3);
                }

                var entryJunk = archive.CreateEntry("readme.txt");
                using (var sJunk = entryJunk.Open())
                {
                    sJunk.Write(new byte[] { 1, 1, 1 }, 0, 3);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 101, Title = "Super Mario Bros", SeriesId = 10 },
                new Episode { Id = 102, Title = "The Legend of Zelda", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(2);
            results.Should().Contain(i => i.Name == "Super Mario Bros (USA)" && i.Path.Contains(".zip#Super Mario Bros (USA).nes"));
            results.Should().Contain(i => i.Name == "The Legend of Zelda (USA)" && i.Path.Contains(".zip#The Legend of Zelda (USA).nes"));
            results.All(i => i.Series != null && i.Series.Title == "Nintendo Entertainment System").Should().BeTrue();
            results.All(i => i.Hashes.ContainsKey("CRC32")).Should().BeTrue();
        }

        [Test]
        public void Scan_ShouldIgnoreNonGameFilesAndJunkInsideDirectories()
        {
            var romPath = Path.Combine(_tempPath, "Mega Man 2 (USA).nes");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            var junkVideo = Path.Combine(_tempPath, "trailer.mp4");
            File.WriteAllBytes(junkVideo, new byte[] { 4, 5, 6 });

            var junkText = Path.Combine(_tempPath, "info.nfo");
            File.WriteAllBytes(junkText, new byte[] { 7, 8, 9 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 103, Title = "Mega Man 2", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Mega Man 2");
            results.First().Path.Should().EndWith("Mega Man 2 (USA).nes");
        }

        [Test]
        public void Scan_RegionSubfolders_ShouldAutodetectPlatformFromExclusiveExtension()
        {
            var usaDir = Path.Combine(_tempPath, "USA");
            Directory.CreateDirectory(usaDir);
            var romPath = Path.Combine(usaDir, "Castlevania (USA).nes");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 104, Title = "Castlevania", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Series.Should().NotBeNull();
            results.First().Series.Title.Should().Be("Nintendo Entertainment System");
            results.First().Episodes.First().Title.Should().Be("Castlevania");
        }

        [Test]
        public void Scan_NestedZipCollectionArchive_ShouldExtractInnerRomDetailsAndIdentifyPlatform()
        {
            var innerZipMemory = new MemoryStream();
            using (var innerArchive = new ZipArchive(innerZipMemory, ZipArchiveMode.Create, true))
            {
                var romEntry = innerArchive.CreateEntry("Metroid (USA).nes");
                using (var romStream = romEntry.Open())
                {
                    romStream.Write(new byte[] { 11, 22, 33, 44 }, 0, 4);
                }
            }

            innerZipMemory.Position = 0;
            var collectionZipPath = Path.Combine(_tempPath, "NES 1G1R Collection.zip");
            using (var outerArchive = ZipFile.Open(collectionZipPath, ZipArchiveMode.Create))
            {
                var nestedZipEntry = outerArchive.CreateEntry("Metroid (USA).zip");
                using (var outerStream = nestedZipEntry.Open())
                {
                    innerZipMemory.CopyTo(outerStream);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 105, Title = "Metroid", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Metroid (USA)");
            results.First().Series.Should().NotBeNull();
            results.First().Series.Title.Should().Be("Nintendo Entertainment System");
            results.First().Episodes.First().Title.Should().Be("Metroid");
            results.First().Hashes.Should().ContainKey("CRC32");
        }

        [Test]
        public void Scan_MultiDiscCollectionZip_ShouldImportEachGameDiscSeparately()
        {
            var collectionZipPath = Path.Combine(_tempPath, "Sega CD Collection.zip");
            using (var archive = ZipFile.Open(collectionZipPath, ZipArchiveMode.Create))
            {
                var entry1 = archive.CreateEntry("Sonic CD (USA)/Sonic CD (USA).cue");
                using (var s1 = entry1.Open())
                {
                    s1.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                }

                var entry2 = archive.CreateEntry("Sonic CD (USA)/Sonic CD (USA) (Track 1).bin");
                using (var s2 = entry2.Open())
                {
                    s2.Write(new byte[] { 5, 6, 7, 8 }, 0, 4);
                }

                var entry3 = archive.CreateEntry("Lunar (USA)/Lunar (USA).cue");
                using (var s3 = entry3.Open())
                {
                    s3.Write(new byte[] { 9, 10, 11, 12 }, 0, 4);
                }

                var entry4 = archive.CreateEntry("Lunar (USA)/Lunar (USA) (Track 1).bin");
                using (var s4 = entry4.Open())
                {
                    s4.Write(new byte[] { 13, 14, 15, 16 }, 0, 4);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 20, Title = "Sega CD", CleanTitle = "segacd", TitleSlug = "sega-cd" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(20)).Returns(new List<Episode>
            {
                new Episode { Id = 201, Title = "Sonic CD", SeriesId = 20 },
                new Episode { Id = 202, Title = "Lunar", SeriesId = 20 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(2);
            results.Should().Contain(r => r.Name == "Sonic CD (USA)");
            results.Should().Contain(r => r.Name == "Lunar (USA)");
        }

        [Test]
        public void Scan_HashMatchedFileWithCompletelyWrongFilename_ShouldUseDatAsAuthorityForTitleRegionAndVersion()
        {
            var romPath = Path.Combine(_tempPath, "totally_wrong_name (Japan) (Beta).nes");
            var payload = new byte[] { 10, 20, 30, 40 };
            File.WriteAllBytes(romPath, payload);

            string sha1Hex;
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                sha1Hex = System.BitConverter.ToString(sha1.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
            }

            var datEntry = new DatEntry
            {
                GameName = "Super Mario Bros. (Europe) (Rev 1)",
                RomName = "Super Mario Bros. (Europe) (Rev 1).nes",
                Sha1 = sha1Hex
            };

            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindPlatformByHash(It.IsAny<string>(), sha1Hex, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DatEntryWithPlatform
                {
                    DatEntry = datEntry,
                    PlatformSlug = "nes"
                });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });

            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 1001, Title = "Super Mario Bros.", SeriesId = 10, SeasonNumber = 2, FinaleType = "Retail (v1.0)" },
                new Episode { Id = 1002, Title = "Super Mario Bros.", SeriesId = 10, SeasonNumber = 2, FinaleType = "Rev 1" },
                new Episode { Id = 1003, Title = "Super Mario Bros.", SeriesId = 10, SeasonNumber = 3, FinaleType = "Retail (v1.0)" }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            var item = results.First();
            item.IsHighConfidenceMatch.Should().BeTrue();
            item.Name.Should().Be("Super Mario Bros.");
            item.SeasonNumber.Should().Be(2);
            item.Episodes.Should().ContainSingle();
            item.Episodes[0].Id.Should().Be(1002);
            item.Episodes[0].FinaleType.Should().Be("Rev 1");
        }

        [Test]
        public void ComputeFileOrArchiveHashes_3doZipWithCueBinAndIaJunk_ShouldVerifyBinTrackAgainstDatWithoutExtracting()
        {
            var zipPath = Path.Combine(_tempPath, "Crash 'n Burn (USA).zip");
            var cueBytes = Encoding.UTF8.GetBytes("FILE \"Crash 'n Burn (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            var binBytes = new byte[4096];
            new Random(42).NextBytes(binBytes);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using (var s = archive.CreateEntry("crash_n_burn_meta.xml").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("<metadata><title>Crash 'n Burn</title></metadata>"));
                }

                using (var s = archive.CreateEntry("Crash 'n Burn (USA).cue").Open())
                {
                    s.Write(cueBytes, 0, cueBytes.Length);
                }

                using (var s = archive.CreateEntry("Crash 'n Burn (USA).bin").Open())
                {
                    s.Write(binBytes, 0, binBytes.Length);
                }

                using (var s = archive.CreateEntry("crash_n_burn_files.xml").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("<files/>"));
                }

                using (var s = archive.CreateEntry("crash_n_burn_archive.torrent").Open())
                {
                    s.Write(new byte[] { 1, 2, 3 });
                }

                using (var s = archive.CreateEntry("cover.jpg").Open())
                {
                    s.Write(new byte[] { 0xFF, 0xD8, 0xFF });
                }

                using (var s = archive.CreateEntry("readme.txt").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("Uploaded to Internet Archive"));
                }
            }

            string binCrc;
            string binSha1;
            string binMd5;
            using (var reopened = ZipFile.OpenRead(zipPath))
            {
                binCrc = reopened.GetEntry("Crash 'n Burn (USA).bin").Crc32.ToString("x8").ToLowerInvariant();
            }

            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                binSha1 = BitConverter.ToString(sha1.ComputeHash(binBytes)).Replace("-", string.Empty).ToLowerInvariant();
            }

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                binMd5 = BitConverter.ToString(md5.ComputeHash(binBytes)).Replace("-", string.Empty).ToLowerInvariant();
            }

            var datCache = Mocker.GetMock<IDatCacheService>();
            datCache.Setup(d => d.FindByCrc32(binCrc)).Returns(new DatEntry
            {
                GameName = "Crash 'n Burn (USA)",
                RomName = "Crash 'n Burn (USA).bin",
                Crc32 = binCrc,
                Sha1 = binSha1,
                Md5 = binMd5
            });

            var hashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(zipPath, new[] { ".chd", ".iso", ".cue", ".bin" }, datCache.Object);

            hashes.Should().ContainKey("CRC32").WhoseValue.Should().Be(binCrc);
            hashes.Should().ContainKey("SHA-1").WhoseValue.Should().Be(binSha1);
            hashes.Should().ContainKey("MD5").WhoseValue.Should().Be(binMd5);
            hashes.Should().ContainKey("ROM_NAME").WhoseValue.Should().Be("Crash 'n Burn (USA).bin");
            Directory.GetFiles(_tempPath).Should().ContainSingle().Which.Should().Be(zipPath);
        }

        [Test]
        public void ComputeFileOrArchiveHashes_JunkOnlyZip_ShouldReturnEmptyHashes()
        {
            var zipPath = Path.Combine(_tempPath, "JunkOnly (USA).zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using (var s = archive.CreateEntry("item_meta.xml").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("<metadata/>"));
                }

                using (var s = archive.CreateEntry("readme.txt").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("Just text"));
                }
            }

            var hashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(zipPath);
            hashes.Should().BeEmpty();
        }

        [Test]
        public void ComputeFileOrArchiveHashes_LooseCueWithUnmatchedCueHash_ShouldFallbackToReferencedBinTrack()
        {
            var cuePath = Path.Combine(_tempPath, "Road Rash (USA).cue");
            var binPath = Path.Combine(_tempPath, "Road Rash (USA).bin");
            var binPayload = new byte[2048];
            new Random(7).NextBytes(binPayload);

            File.WriteAllText(cuePath, "FILE \"Road Rash (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            File.WriteAllBytes(binPath, binPayload);

            string binSha1;
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                binSha1 = BitConverter.ToString(sha1.ComputeHash(binPayload)).Replace("-", string.Empty).ToLowerInvariant();
            }

            var datCache = Mocker.GetMock<IDatCacheService>();
            datCache.Setup(d => d.FindBySha1(binSha1)).Returns(new DatEntry
            {
                GameName = "Road Rash (USA)",
                RomName = "Road Rash (USA).bin",
                Sha1 = binSha1
            });

            var hashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(cuePath, new[] { ".chd", ".iso", ".cue", ".bin" }, datCache.Object);

            hashes.Should().ContainKey("SHA-1").WhoseValue.Should().Be(binSha1);
            hashes.Should().ContainKey("ROM_NAME").WhoseValue.Should().Be("Road Rash (USA).cue");
        }

        [Test]
        public void ComputeFileOrArchiveHashes_CorruptRomWithMatchingSfv_ShouldComputeActualFileHash()
        {
            var romPath = Path.Combine(_tempPath, "Super Mario World (USA).sfc");
            var sfvPath = Path.Combine(_tempPath, "release.sfv");

            var actualBytes = Encoding.ASCII.GetBytes("CORRUPTED_ROM_PAYLOAD");
            File.WriteAllBytes(romPath, actualBytes);

            var fakeSfvCrc = "b19ed489";
            File.WriteAllText(sfvPath, $"Super Mario World (USA).sfc {fakeSfvCrc}\r\n");

            var datCache = Mocker.GetMock<IDatCacheService>();
            datCache.Setup(d => d.FindByCrc32(fakeSfvCrc)).Returns(new DatEntry
            {
                GameName = "Super Mario World (USA)",
                RomName = "Super Mario World (USA).sfc",
                Crc32 = fakeSfvCrc,
                Sha1 = "da39a3ee5e6b4b0d3255bfef95601890afd80709"
            });

            var hashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(romPath, new[] { ".sfc" }, datCache.Object);

            hashes.Should().ContainKey("SFV_CRC32").WhoseValue.Should().Be(fakeSfvCrc);
            hashes.Should().ContainKey("CRC32").WhoseValue.Should().NotBe(fakeSfvCrc);
            hashes.Should().ContainKey("SHA-1").WhoseValue.Should().NotBe("da39a3ee5e6b4b0d3255bfef95601890afd80709");
        }
    }
}
