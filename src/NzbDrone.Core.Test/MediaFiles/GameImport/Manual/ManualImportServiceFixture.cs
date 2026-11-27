using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.GameImport.Manual;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.GameImport.Manual
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "mis_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void should_deduplicate_companion_bin_when_submitted_with_cue_in_manual_import_command()
        {
            var cuePath = Path.Combine(_tempDir, "Game (USA).cue");
            var binPath = Path.Combine(_tempDir, "Game (USA).bin");
            File.WriteAllText(cuePath, "FILE \"Game (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            File.WriteAllBytes(binPath, new byte[4096]);

            var libraryPath = Path.Combine(_tempDir, "Library");
            Directory.CreateDirectory(libraryPath);

            var platform = new Series { Id = 1, Title = "Philips CD-i", TitleSlug = "cdi", Path = libraryPath };
            var game = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "The Apprentice", SeriesId = 1 };

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(platform);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(new List<Episode> { game });
            Mocker.GetMock<NzbDrone.Core.MediaFiles.GameImport.Aggregation.IAggregationService>()
                .Setup(a => a.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                .Returns<LocalEpisode, DownloadClientItem>((le, dl) => le);

            var importedPaths = new List<string>();
            Mocker.GetMock<IImportApprovedGames>()
                .Setup(i => i.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Move))
                .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDl, dl, mode) =>
                {
                    importedPaths.Add(decisions[0].LocalEpisode.Path);
                    return new List<ImportResult>
                    {
                        new ImportResult(decisions[0], new EpisodeFile { SeriesId = 1, SeasonNumber = 1, RelativePath = "Game (USA).cue" })
                    };
                });

            var command = new ManualImportCommand
            {
                ImportMode = ImportMode.Move,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile { Path = cuePath, SeriesId = 1, GameIds = new List<int> { 10 }, Quality = new QualityModel(Quality.VerifiedGoodDump) },
                    new ManualImportFile { Path = binPath, SeriesId = 1, GameIds = new List<int> { 10 }, Quality = new QualityModel(Quality.VerifiedGoodDump) }
                }
            };

            Subject.Execute(command);

            importedPaths.Should().ContainSingle().Which.Should().Be(cuePath);
        }

        [Test]
        public void should_extract_nested_archives_recursively_when_scanning_file_for_manual_import()
        {
            var tarGzPath = Path.Combine(_tempDir, "inner.tar.gz");
            using (var fs = File.Create(tarGzPath))
            using (var gz = new GZipStream(fs, CompressionMode.Compress))
            using (var tar = new TarWriter(gz, TarEntryFormat.Pax))
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, "Super Mario World (USA).sfc")
                {
                    DataStream = new MemoryStream(new byte[] { 1, 2, 3, 4 })
                };
                tar.WriteEntry(entry);
            }

            var outerZipPath = Path.Combine(_tempDir, "SuperMarioWorld.zip");
            using (var zip = ZipFile.Open(outerZipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tarGzPath, "nested/inner.tar.gz");
            }

            File.Delete(tarGzPath);

            var expectedExtractDir = Path.Combine(_tempDir, "SuperMarioWorld_extracted");

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(It.IsAny<string>())).Returns<string>(p => Directory.Exists(p));
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(It.IsAny<string>())).Returns<string>(p => File.Exists(p));
            Mocker.GetMock<IUnmanagedDiskScanner>().Setup(s => s.Scan(It.Is<string>(p => Directory.Exists(p))))
                .Returns<string>(dir => Directory.GetFiles(dir, "*.sfc", SearchOption.AllDirectories)
                    .Select(f => new ManualImportItem
                    {
                        Path = f,
                        Name = Path.GetFileNameWithoutExtension(f)
                    })
                    .ToList());

            var items = Subject.GetMediaFiles(outerZipPath, null, null, false);

            Directory.Exists(expectedExtractDir).Should().BeTrue();
            items.Should().ContainSingle();
            items[0].Path.Should().EndWith("Super Mario World (USA).sfc");
        }

        [Test]
        public void should_allow_post_process_compression_for_disc_and_archive_files_when_enabled()
        {
            var libraryPath = Path.Combine(_tempDir, "Library");
            Directory.CreateDirectory(libraryPath);
            var existingCuePath = Path.Combine(libraryPath, "Game (USA)", "Game (USA).cue");
            Directory.CreateDirectory(Path.GetDirectoryName(existingCuePath));
            File.WriteAllText(existingCuePath, "FILE \"Game (USA).bin\" BINARY\r\n");

            var ps1Compressed = new Series { Id = 1, Title = "Sony PlayStation", TitleSlug = "psx", Path = libraryPath, PreferredArchiveFormat = "auto" };
            var ps1Uncompressed = new Series { Id = 2, Title = "Sony PlayStation", TitleSlug = "psx", Path = libraryPath, PreferredArchiveFormat = "none" };
            var game = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "Game", SeriesId = 1 };

            RomFileMovingService.ShouldPostProcessInPlace(existingCuePath, ps1Compressed).Should().BeTrue();
            RomFileMovingService.ShouldPostProcessInPlace(Path.Combine(_tempDir, "Game (USA).7z"), ps1Compressed).Should().BeTrue();
            RomFileMovingService.ShouldPostProcessInPlace(existingCuePath, ps1Uncompressed).Should().BeFalse();

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(ps1Compressed);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(new List<Episode> { game });
            Mocker.GetMock<NzbDrone.Core.MediaFiles.GameImport.Aggregation.IAggregationService>()
                .Setup(a => a.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                .Returns<LocalEpisode, DownloadClientItem>((le, _) => le);

            var importedDecisions = new List<ImportDecision>();
            Mocker.GetMock<IImportApprovedGames>()
                .Setup(i => i.Import(It.IsAny<List<ImportDecision>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), ImportMode.Move))
                .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, _, _, _) =>
                {
                    importedDecisions.AddRange(decisions);
                    return new List<ImportResult>
                    {
                        new ImportResult(decisions[0], new EpisodeFile { SeriesId = 1, SeasonNumber = 1, RelativePath = "Game (USA).chd" })
                    };
                });

            var command = new ManualImportCommand
            {
                ImportMode = ImportMode.Move,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile { Path = existingCuePath, SeriesId = 1, GameIds = new List<int> { 10 }, Quality = new QualityModel(Quality.VerifiedGoodDump) }
                }
            };

            Subject.Execute(command);

            importedDecisions.Should().ContainSingle().Which.LocalEpisode.Path.Should().Be(existingCuePath);
        }

        [Test]
        public void should_mark_tracked_download_imported_and_cleanup_extracted_folder_when_manual_import_completes()
        {
            var extractDir = Path.Combine(_tempDir, "GameRelease_extracted");
            Directory.CreateDirectory(extractDir);
            var romPath = Path.Combine(extractDir, "Game (USA).sfc");
            File.WriteAllBytes(romPath, new byte[128]);

            var libraryPath = Path.Combine(_tempDir, "Library");
            Directory.CreateDirectory(libraryPath);

            var platform = new Series { Id = 1, Title = "Super Nintendo", TitleSlug = "snes", Path = libraryPath };
            var game1 = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "Game", SeriesId = 1 };
            var game2 = new Episode { Id = 11, SeasonNumber = 1, EpisodeNumber = 2, Title = "Game Bonus", SeriesId = 1 };

            var trackedDownload = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem { DownloadId = "ia-dl-1", Title = "Game (USA)", OutputPath = new OsPath(Path.Combine(_tempDir, "GameRelease.zip")) },
                ImportItem = new DownloadClientItem { DownloadId = "ia-dl-1", Title = "Game (USA)", OutputPath = new OsPath(Path.Combine(_tempDir, "GameRelease.zip")) },
                RemoteEpisode = new RemoteEpisode { Series = platform, Episodes = new List<Episode> { game1, game2 } },
                State = TrackedDownloadState.ImportPending
            };

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(platform);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(new List<Episode> { game1 });
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("ia-dl-1")).Returns(trackedDownload);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(It.IsAny<string>())).Returns<string>(Directory.Exists);
            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.DeleteFolder(It.IsAny<string>(), true))
                .Callback<string, bool>((p, _) =>
                {
                    if (Directory.Exists(p))
                    {
                        Directory.Delete(p, true);
                    }
                });
            Mocker.GetMock<NzbDrone.Core.MediaFiles.GameImport.Aggregation.IAggregationService>()
                .Setup(a => a.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                .Returns<LocalEpisode, DownloadClientItem>((le, _) => le);

            Mocker.GetMock<IImportApprovedGames>()
                .Setup(i => i.Import(It.IsAny<List<ImportDecision>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), ImportMode.Move))
                .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, _, _, _) =>
                    new List<ImportResult>
                    {
                        new ImportResult(decisions[0], new EpisodeFile { SeriesId = 1, SeasonNumber = 1, RelativePath = "Game (USA).sfc" })
                    });

            var command = new ManualImportCommand
            {
                ImportMode = ImportMode.Move,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile
                    {
                        Path = romPath,
                        SeriesId = 1,
                        GameIds = new List<int> { 10 },
                        Quality = new QualityModel(Quality.NoIntroVerified),
                        DownloadId = "ia-dl-1"
                    }
                }
            };

            Subject.Execute(command);

            trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
            Directory.Exists(extractDir).Should().BeFalse();
            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<DownloadCompletedEvent>(ev => ev.TrackedDownload == trackedDownload)),
                Times.Once());
        }
    }
}
