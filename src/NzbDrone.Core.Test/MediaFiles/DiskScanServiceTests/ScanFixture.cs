using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.DiskScanServiceTests
{
    [TestFixture]
    public class ScanFixture : CoreTest<DiskScanService>
    {
        private Series _series;
        private string _rootFolder;
        private string _otherSeriesFolder;

        [SetUp]
        public void Setup()
        {
            _rootFolder = @"C:\Test\TV".AsOsAgnostic();
            _otherSeriesFolder = @"C:\Test\TV\OtherSeries".AsOsAgnostic();
            var seriesFolder = @"C:\Test\TV\Series".AsOsAgnostic();

            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Path = seriesFolder)
                                     .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetParentFolder(It.IsAny<string>()))
                  .Returns((string path) => Directory.GetParent(path).FullName);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                  .Returns(_rootFolder);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFilesBySeries(It.IsAny<int>()))
                  .Returns(new List<EpisodeFile>());
        }

        private void GivenRootFolder(params string[] subfolders)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_rootFolder))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(_rootFolder))
                  .Returns(subfolders);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(_rootFolder))
                  .Returns(subfolders.Empty());

            foreach (var folder in subfolders)
            {
                Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(folder))
                  .Returns(true);
            }
        }

        private void GivenSeriesFolder()
        {
            GivenRootFolder(_series.Path);
        }

        private void GivenFiles(IEnumerable<string> files)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                  .Returns(files.ToArray());
        }

        [Test]
        public void should_not_scan_if_root_folder_does_not_exist()
        {
            Subject.Scan(_series);

            ExceptionVerification.ExpectedWarns(1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(_series.Path, true), Times.Never());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Never());

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Never());
        }

        [Test]
        public void should_not_scan_if_series_root_folder_is_empty()
        {
            GivenRootFolder();

            Subject.Scan(_series);

            ExceptionVerification.ExpectedWarns(1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(_series.Path, true), Times.Never());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Never());

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Never());

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.IsAny<List<string>>(), _series, false), Times.Never());
        }

        [Test]
        public void should_create_if_series_folder_does_not_exist_but_create_folder_enabled()
        {
            GivenRootFolder(_otherSeriesFolder);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CreateEmptySeriesFolders)
                  .Returns(true);

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Once());
        }

        [Test]
        public void should_not_create_if_series_folder_does_not_exist_and_create_folder_disabled()
        {
            GivenRootFolder(_otherSeriesFolder);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CreateEmptySeriesFolders)
                  .Returns(false);

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Never());
        }

        [Test]
        public void should_clean_but_not_import_if_series_folder_does_not_exist()
        {
            GivenRootFolder(_otherSeriesFolder);

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.FolderExists(_series.Path), Times.Once());

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Once());

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.IsAny<List<string>>(), _series, false), Times.Never());
        }

        [Test]
        public void should_not_scan_various_extras_subfolders()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Behind the Scenes", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Deleted Scenes", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Featurettes", "file3.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Interviews", "file4.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Samples", "file5.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Scenes", "file6.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Shorts", "file7.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Trailers", "file8.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Other", "file9.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Series Title S01E01 (1080p BluRay x265 10bit Tigole).nes").AsOsAgnostic(),
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_featurettes_subfolders()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Featurettes", "An Epic Reborn.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Featurettes", "Deleted & Alternate Scenes.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Featurettes", "En Garde - Multi-Angle Dailies.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Featurettes", "Layer-By-Layer - Sound Design - Multiple Audio.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Series Title S01E01 (1080p BluRay x265 10bit Tigole).nes").AsOsAgnostic(),
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_clean_but_not_import_if_series_folder_does_not_exist_and_create_folder_enabled()
        {
            GivenRootFolder(_otherSeriesFolder);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CreateEmptySeriesFolders)
                  .Returns(true);

            Subject.Scan(_series);

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Once());

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.IsAny<List<string>>(), _series, false), Times.Never());
        }

        [Test]
        public void should_find_files_at_root_of_series_folder()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 2), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_extras_subfolder()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "EXTRAS", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Extras", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "EXTRAs", "file3.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "ExTrAs", "file4.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Exactly(3));

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_AppleDouble_subfolder()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, ".AppleDouble", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, ".appledouble", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_scan_extras_series_and_subfolders()
        {
            _series.Path = @"C:\Test\TV\Extras".AsOsAgnostic();

            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Extras", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, ".AppleDouble", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e02.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 2", "s02e01.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 2", "s02e02.nes").AsOsAgnostic(),
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 4), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_subfolders_that_start_with_period()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, ".@__thumb", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, ".@__THUMB", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, ".hidden", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_scan_files_that_start_with_period()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Season 1", ".s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_subfolder_of_season_folder_that_starts_with_a_period()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Season 1", ".@__thumb", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", ".@__THUMB", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", ".hidden", "file2.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", ".AppleDouble", "s01e01.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_Synology_eaDir()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "@eaDir", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_thumb_folder()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, ".@__thumb", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_scan_dotHack_folder()
        {
            _series.Path = @"C:\Test\TV\.hack".AsOsAgnostic();

            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Season 1", "file1.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Season 1", "s01e01.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 2), _series, false), Times.Once());
        }

        [Test]
        public void should_exclude_inline_extra_files()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "Series Title S01E01.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "Deleted Scenes-deleted.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "The World of Pandora-other.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_exclude_osx_metadata_files()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
                       {
                           Path.Combine(_series.Path, "._24 The Status Quo Combustion.nes").AsOsAgnostic(),
                           Path.Combine(_series.Path, "24 The Status Quo Combustion.nes").AsOsAgnostic()
                       });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());
        }

        [Test]
        public void should_not_scan_excluded_files()
        {
            GivenSeriesFolder();

            GivenFiles(new List<string>
            {
                Path.Combine(_series.Path, ".DS_Store").AsOsAgnostic(),
                Path.Combine(_series.Path, ".unmanic").AsOsAgnostic(),
                Path.Combine(_series.Path, ".unmanic.part").AsOsAgnostic(),
                Path.Combine(_series.Path, "24 The Status Quo Combustion.nes").AsOsAgnostic()
            });

            Subject.Scan(_series);

            Mocker.GetMock<IMakeImportDecision>()
                .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Count == 1), _series, false), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                .Verify(v => v.PublishEvent(It.Is<PlatformScannedEvent>(c => c.Series != null && c.PossibleExtraFiles.Count == 0)), Times.Once());
        }

        [Test]
        public void should_extract_collection_archive_with_multiple_cue_discs()
        {
            GivenSeriesFolder();

            var tempZip = Path.Combine(Path.GetTempPath(), "SegaCD_Collection_" + Guid.NewGuid() + ".zip");
            try
            {
                using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
                {
                    var e1 = archive.CreateEntry("Sonic CD (USA)/Sonic CD.cue");
                    using (var s1 = e1.Open())
                    {
                        s1.Write(new byte[] { 1, 2, 3 }, 0, 3);
                    }

                    var e2 = archive.CreateEntry("Sonic CD (USA)/Sonic CD (Track 1).bin");
                    using (var s2 = e2.Open())
                    {
                        s2.Write(new byte[] { 4, 5, 6 }, 0, 3);
                    }

                    var e3 = archive.CreateEntry("Lunar (USA)/Lunar.cue");
                    using (var s3 = e3.Open())
                    {
                        s3.Write(new byte[] { 7, 8, 9 }, 0, 3);
                    }

                    var e4 = archive.CreateEntry("Lunar (USA)/Lunar (Track 1).bin");
                    using (var s4 = e4.Open())
                    {
                        s4.Write(new byte[] { 10, 11, 12 }, 0, 3);
                    }
                }

                _series.Title = "Sega CD";
                _series.CleanTitle = "Sega CD";

                Mocker.GetMock<IDiskProvider>()
                    .Setup(s => s.GetFiles(_series.Path, true))
                    .Returns(new[] { tempZip });

                Subject.Scan(_series);

                Mocker.GetMock<IDiskProvider>()
                    .Verify(s => s.SaveStream(It.IsAny<Stream>(), It.Is<string>(p => p.EndsWith("Sonic CD.cue"))), Times.Once());

                Mocker.GetMock<IDiskProvider>()
                    .Verify(s => s.SaveStream(It.IsAny<Stream>(), It.Is<string>(p => p.EndsWith("Lunar.cue"))), Times.Once());
            }
            finally
            {
                if (File.Exists(tempZip))
                {
                    File.Delete(tempZip);
                }
            }
        }

        [Test]
        public void should_not_extract_single_rom_archive_during_scan()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SeriesScan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempZip = Path.Combine(tempDir, "Super Mario Bros (USA).zip");

            try
            {
                using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Super Mario Bros (USA).nes");
                    using var stream = entry.Open();
                    stream.Write(new byte[] { 0x4E, 0x45, 0x53, 0x1A }, 0, 4);
                }

                _series.Path = tempDir;
                _series.Title = "Nintendo Entertainment System";
                _series.CleanTitle = "Nintendo Entertainment System";

                GivenSeriesFolder();

                Mocker.GetMock<IDiskProvider>()
                    .Setup(s => s.GetFiles(_series.Path, true))
                    .Returns(new[] { tempZip });

                Subject.Scan(_series);

                Mocker.GetMock<IDiskProvider>()
                    .Verify(s => s.SaveStream(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never());

                Mocker.GetMock<IMakeImportDecision>()
                    .Verify(v => v.GetImportDecisions(It.Is<List<string>>(l => l.Contains(tempZip)), _series, false), Times.Once());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
