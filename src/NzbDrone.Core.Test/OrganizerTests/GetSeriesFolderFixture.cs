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

            Mocker.GetMock<NzbDrone.Core.Qualities.IQualityDefinitionService>()
                  .Setup(c => c.Get(Moq.It.IsAny<NzbDrone.Core.Qualities.Quality>()))
                  .Returns(new NzbDrone.Core.Qualities.QualityDefinition { Title = "Verified" });

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

            Mocker.GetMock<NzbDrone.Core.Qualities.IQualityDefinitionService>()
                  .Setup(c => c.Get(Moq.It.IsAny<NzbDrone.Core.Qualities.Quality>()))
                  .Returns(new NzbDrone.Core.Qualities.QualityDefinition { Title = "Verified" });

            var romInfo = RomParser.ParseRomName("Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.NoIntroStandard)
                   .Should().Be("Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.SmokeMonsterHtgdb)
                   .Should().Be("USA/A-F/Chrono Trigger (USA) (Rev A).sfc");

            Subject.BuildRomFileName(romInfo, "snes", RomNamingPreset.FlatConsole)
                   .Should().Be("Nintendo/Super Nintendo/Chrono Trigger (USA).sfc");
        }

        [Test]
        public void should_resolve_unified_console_region_and_game_naming_tokens()
        {
            Mocker.GetMock<IPlatformCatalogService>()
                  .Setup(c => c.GetAll())
                  .Returns(new PlatformCatalogService().GetAll());

            Mocker.GetMock<NzbDrone.Core.Qualities.IQualityDefinitionService>()
                  .Setup(c => c.Get(Moq.It.IsAny<NzbDrone.Core.Qualities.Quality>()))
                  .Returns(new NzbDrone.Core.Qualities.QualityDefinition { Title = "Verified" });

            _namingConfig.RenameEpisodes = true;
            _namingConfig.SeriesFolderFormat = "{Platform Short}";
            _namingConfig.SeasonFolderFormat = "{Region Code}";
            _namingConfig.StandardEpisodeFormat = "{Game CleanTitle} ({Release Year}) ({Version}) ({Disc})";

            var series = new Series
            {
                Title = "PlayStation",
                TitleSlug = "psx",
                Year = 1995,
                Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Title = "USA" }
                }
            };

            var episode = new Episode
            {
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Final Fantasy VII (USA) (Rev 1) (Disc 1)",
                AirDate = "1997-09-07"
            };

            var episodeFile = new NzbDrone.Core.MediaFiles.EpisodeFile
            {
                Quality = new NzbDrone.Core.Qualities.QualityModel(NzbDrone.Core.Qualities.Quality.Unknown),
                SceneName = "Final Fantasy VII (USA) (Rev 1) (Disc 1)"
            };

            Subject.GetSeriesFolder(series, _namingConfig).Should().Be("PS1");
            Subject.GetSeasonFolder(series, 1, _namingConfig).Should().Be("US");
            Subject.BuildFileName(new List<Episode> { episode }, series, episodeFile, ".chd", _namingConfig, new List<NzbDrone.Core.CustomFormats.CustomFormat>())
                   .Should().Be("Final Fantasy VII USA Rev 1 Disc 1 (1997) (Rev 1) (Disc 1).chd");
        }
    }
}
