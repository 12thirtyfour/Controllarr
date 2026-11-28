using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class RomFileMovingServiceCollisionFixture : CoreTest<RomFileMovingService>
    {
        [Test]
        public void should_disambiguate_destination_when_another_rom_variant_already_occupies_target_path()
        {
            var platformDir = @"C:\Roms\GCE\Vectrex".AsOsAgnostic();
            var regionDir = Path.Combine(platformDir, "World");
            var sourcePath = Path.Combine(regionDir, "Blitz! - Action Football (USA, Europe) (881B27C5).zip");
            var collidingDest = Path.Combine(regionDir, "Blitz! - Action Football.zip");
            var expectedDisambiguatedDest = Path.Combine(regionDir, "Blitz! - Action Football (881B27C5).zip");

            var platform = new Series
            {
                Id = 137,
                Title = "GCE Vectrex",
                TitleSlug = "vectrex",
                Path = platformDir,
                SeasonFolder = true,
                PreferredArchiveFormat = "zip"
            };

            var gameVariant = new Episode
            {
                Id = 422521,
                SeriesId = 137,
                SeasonNumber = 4,
                Title = "Blitz! - Action Football",
                FinaleType = "Retail (v1.0)"
            };

            var romFile = new EpisodeFile
            {
                Id = 0,
                SeriesId = 137,
                RelativePath = Path.Combine("World", "Blitz! - Action Football (USA, Europe) (881B27C5).zip"),
                Path = sourcePath
            };

            var localGame = new LocalEpisode
            {
                Path = sourcePath,
                Series = platform,
                Episodes = new List<Episode> { gameVariant },
                RomHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CRC32"] = "881b27c5"
                }
            };

            Mocker.GetMock<IBuildFileNames>()
                .Setup(b => b.BuildFilePath(It.IsAny<List<Episode>>(), platform, romFile, ".zip", null, It.IsAny<List<NzbDrone.Core.CustomFormats.CustomFormat>>()))
                .Returns(collidingDest);

            Mocker.GetMock<IBuildFileNames>()
                .Setup(b => b.BuildSeasonPath(platform, 4))
                .Returns(regionDir);

            Mocker.GetMock<IRootFolderService>()
                .Setup(r => r.GetBestRootFolderPath(platformDir))
                .Returns(@"C:\Roms".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FileExists(sourcePath))
                .Returns(true);
            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FileExists(collidingDest))
                .Returns(true);
            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FileExists(expectedDisambiguatedDest))
                .Returns(false);

            Mocker.GetMock<IImportScript>()
                .Setup(s => s.TryImport(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LocalEpisode>(), It.IsAny<EpisodeFile>(), It.IsAny<TransferMode>()))
                .Returns(ScriptImportDecision.DeferMove);

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            result.RelativePath.Should().Be(Path.Combine("World", "Blitz! - Action Football (881B27C5).zip"));

            Mocker.GetMock<IDiskTransferService>()
                .Verify(t => t.TransferFile(sourcePath, expectedDisambiguatedDest, TransferMode.Move, false), Times.Once());
            Mocker.GetMock<IDiskTransferService>()
                .Verify(t => t.TransferFile(sourcePath, collidingDest, It.IsAny<TransferMode>(), true), Times.Never());
        }

        [Test]
        public void should_not_append_disambiguator_when_destination_does_not_exist()
        {
            var platformDir = @"C:\Roms\GCE\Vectrex".AsOsAgnostic();
            var regionDir = Path.Combine(platformDir, "World");
            var sourcePath = Path.Combine(regionDir, "Clean Sweep (World).zip");
            var expectedDest = Path.Combine(regionDir, "Clean Sweep.zip");

            var platform = new Series
            {
                Id = 137,
                Title = "GCE Vectrex",
                TitleSlug = "vectrex",
                Path = platformDir,
                SeasonFolder = true,
                PreferredArchiveFormat = "zip"
            };

            var game = new Episode
            {
                Id = 422522,
                SeriesId = 137,
                SeasonNumber = 4,
                Title = "Clean Sweep",
                FinaleType = "Retail (v1.0)"
            };

            var romFile = new EpisodeFile
            {
                Id = 0,
                SeriesId = 137,
                RelativePath = Path.Combine("World", "Clean Sweep (World).zip"),
                Path = sourcePath
            };

            var localGame = new LocalEpisode
            {
                Path = sourcePath,
                Series = platform,
                Episodes = new List<Episode> { game },
                RomHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CRC32"] = "12345678"
                }
            };

            Mocker.GetMock<IBuildFileNames>()
                .Setup(b => b.BuildFilePath(It.IsAny<List<Episode>>(), platform, romFile, ".zip", null, It.IsAny<List<NzbDrone.Core.CustomFormats.CustomFormat>>()))
                .Returns(expectedDest);

            Mocker.GetMock<IBuildFileNames>()
                .Setup(b => b.BuildSeasonPath(platform, 4))
                .Returns(regionDir);

            Mocker.GetMock<IRootFolderService>()
                .Setup(r => r.GetBestRootFolderPath(platformDir))
                .Returns(@"C:\Roms".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FileExists(sourcePath))
                .Returns(true);
            Mocker.GetMock<IDiskProvider>()
                .Setup(d => d.FileExists(expectedDest))
                .Returns(false);

            Mocker.GetMock<IImportScript>()
                .Setup(s => s.TryImport(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LocalEpisode>(), It.IsAny<EpisodeFile>(), It.IsAny<TransferMode>()))
                .Returns(ScriptImportDecision.DeferMove);

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            result.RelativePath.Should().Be(Path.Combine("World", "Clean Sweep.zip"));

            Mocker.GetMock<IDiskTransferService>()
                .Verify(t => t.TransferFile(sourcePath, expectedDest, TransferMode.Move, false), Times.Once());
        }
    }
}
