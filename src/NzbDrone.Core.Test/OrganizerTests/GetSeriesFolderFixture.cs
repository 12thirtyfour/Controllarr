using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class GetSeriesFolderFixture : CoreTest<FileNameBuilder>
    {
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _namingConfig = NamingConfig.Default;

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);

            Mocker.GetMock<IPlatformCatalogService>()
                  .Setup(c => c.GetAll()).Returns(new List<PlatformDefinition>());
        }

        [TestCase("30 Rock", "{Series Title}", "30 Rock")]
        [TestCase("30 Rock", "{Series.Title}", "30.Rock")]
        [TestCase("24/7 Road to the NHL Winter Classic", "{Series Title}", "24+7 Road to the NHL Winter Classic")]
        [TestCase("Venture Bros.", "{Series.Title}", "Venture.Bros")]
        [TestCase(".hack", "{Series.Title}", "hack")]
        [TestCase("30 Rock", ".{Series.Title}.", "30.Rock")]
        public void should_use_seriesFolderFormat_to_build_folder_name(string seriesTitle, string format, string expected)
        {
            _namingConfig.SeriesFolderFormat = format;

            var series = new Series { Title = seriesTitle };

            Subject.GetSeriesFolder(series).Should().Be(expected);
        }

        [Test]
        public void should_build_platform_region_path_with_regional_console_names()
        {
            Mocker.GetMock<IPlatformCatalogService>()
                  .Setup(c => c.GetAll())
                  .Returns(new PlatformCatalogService().GetAll());

            var root = Path.Combine("Library", "ROMs");

            Subject.BuildPlatformRegionPath(root, "snes", "US")
                   .Should().Be(Path.Combine(root, "Nintendo", "Super Nintendo", "US"));

            Subject.BuildPlatformRegionPath(root, "snes", "JP")
                   .Should().Be(Path.Combine(root, "Nintendo", "Super Famicom", "JP"));

            Subject.BuildPlatformRegionPath(root, "genesis", "EU")
                   .Should().Be(Path.Combine(root, "Sega", "Mega Drive", "EU"));
        }

        [Test]
        public void should_build_rom_file_names_for_nointro_and_htgdb_presets()
        {
            Mocker.GetMock<IPlatformCatalogService>()
                  .Setup(c => c.GetAll())
                  .Returns(new PlatformCatalogService().GetAll());

            var romInfo = RomParser.ParseRomName("Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.NoIntroStandard)
                   .Should().Be("Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.SmokeMonsterHtgdb)
                   .Should().Be("USA/A-F/Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.FlatConsole)
                   .Should().Be("Nintendo/Super Nintendo/Chrono Trigger (USA).sfc");
        }
    }
}
