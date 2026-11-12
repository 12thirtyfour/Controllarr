using System;
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.PlatformTests
{
    [TestFixture]
    public class AddPlatformFixture : CoreTest<AddPlatformService>
    {
        private Series _fakeSeries;

        [SetUp]
        public void Setup()
        {
            _fakeSeries = Builder<Series>
                .CreateNew()
                .With(s => s.Path = null)
                .Build();
        }

        private void GivenValidSeries(int tvdbId)
        {
            Mocker.GetMock<IProvidePlatformInfo>()
                  .Setup(s => s.GetSeriesInfo(tvdbId, Language.English, It.IsAny<string>()))
                  .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));
        }

        private void GivenValidPath()
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), null))
                  .Returns<Series, NamingConfig>((c, n) => c.Title);

            Mocker.GetMock<IAddPlatformValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult());
        }

        [Test]
        public void should_be_able_to_add_a_series_without_passing_in_title()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                RootFolderPath = @"C:\Test\TV",
                Language = Language.English
            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Title.Should().Be(_fakeSeries.Title);
        }

        [Test]
        public void should_have_proper_path()
        {
            var newSeries = new Series
                            {
                                TvdbId = 1,
                                RootFolderPath = @"C:\Test\TV",
                                Language = Language.English
                            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Path.Should().Be(Path.Combine(newSeries.RootFolderPath, _fakeSeries.Title));
        }

        [Test]
        public void should_throw_if_series_validation_fails()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1",
                Language = Language.English
            };

            GivenValidSeries(newSeries.TvdbId);

            Mocker.GetMock<IAddPlatformValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));
        }

        [Test]
        public void should_throw_if_series_cannot_be_found()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1",
                Language = Language.English
            };

            Mocker.GetMock<IProvidePlatformInfo>()
                  .Setup(s => s.GetSeriesInfo(newSeries.TvdbId, Language.English, It.IsAny<string>()))
                  .Throws(new SeriesNotFoundException(newSeries.TvdbId));

            Mocker.GetMock<IAddPlatformValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_fetch_series_info_using_the_language_on_the_series()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                RootFolderPath = @"C:\Test\TV",
                Language = Language.Spanish
            };

            Mocker.GetMock<IProvidePlatformInfo>()
                  .Setup(s => s.GetSeriesInfo(newSeries.TvdbId, Language.Spanish, It.IsAny<string>()))
                  .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));

            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Language.Should().Be(Language.Spanish);

            Mocker.GetMock<IProvidePlatformInfo>()
                  .Verify(s => s.GetSeriesInfo(newSeries.TvdbId, Language.Spanish, It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_return_existing_platform_if_already_added_by_tvdbid()
        {
            var existing = new Series { Id = 42, Title = "Nintendo 3DS", TvdbId = 1045, TitleSlug = "3ds" };
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTvdbId(1045))
                  .Returns(existing);

            var newSeries = new Series
            {
                TvdbId = 1045,
                Title = "Nintendo 3DS",
                TitleSlug = "3ds"
            };

            var result = Subject.AddSeries(newSeries);

            result.Should().BeSameAs(existing);
            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void should_refresh_existing_platform_and_queue_command_when_importing_list_containing_existing_platform()
        {
            var existing = new Series { Id = 42, Title = "Nintendo Game Boy", TvdbId = 1007, TitleSlug = "gb", Path = @"C:\Games\Game Boy" };
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new Dictionary<int, int> { { 42, 1007 } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTvdbId(1007))
                  .Returns(existing);

            var incoming = new Series
            {
                TvdbId = 1007,
                Title = "Nintendo Game Boy",
                TitleSlug = "gb",
                Path = @"C:\Games\Game Boy.zip"
            };

            var results = Subject.AddSeries(new List<Series> { incoming });

            results.Should().ContainSingle();
            results[0].Should().BeSameAs(existing);

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(q => q.Push(It.Is<RefreshPlatformCommand>(c => c.SeriesIds.Contains(42))), Times.Once());
        }

        [Test]
        public void should_return_existing_platform_if_already_added_by_title()
        {
            var existing = new Series { Id = 99, Title = "Nintendo Entertainment System", TvdbId = 1001, TitleSlug = "nes" };
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle("Nintendo Entertainment System"))
                  .Returns(existing);

            var newSeries = new Series
            {
                TvdbId = 9999,
                Title = "Nintendo Entertainment System",
                TitleSlug = "nes"
            };

            var result = Subject.AddSeries(newSeries);

            result.Should().BeSameAs(existing);
            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void should_resolve_to_primary_library_root_folder_when_importing_from_source_folder()
        {
            var sourcePath = @"C:\Downloads\Super Nintendo";
            var targetRoot = @"C:\Roms";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(sourcePath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(targetRoot))
                  .Returns(true);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(r => r.All())
                  .Returns(new List<RootFolder> { new RootFolder { Id = 1, Path = targetRoot } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(list => list);

            GivenValidSeries(1005);
            GivenValidPath();

            var newSeries = new Series
            {
                TvdbId = 1005,
                Title = "Super Nintendo Entertainment System",
                Path = sourcePath,
                Language = Language.English
            };

            var added = Subject.AddSeries(new List<Series> { newSeries });

            added.Should().ContainSingle();
            added[0].Path.Should().Be(Path.Combine(targetRoot, _fakeSeries.Title));
            added[0].RootFolderPath.Should().Be(targetRoot);
        }

        [Test]
        public void should_leave_platform_in_place_when_import_mode_is_leave()
        {
            var sourcePath = @"C:\Downloads\Super Nintendo";
            var targetRoot = @"C:\Roms";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(sourcePath))
                  .Returns(true);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(r => r.All())
                  .Returns(new List<RootFolder> { new RootFolder { Id = 1, Path = targetRoot } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(list => list);

            GivenValidSeries(1005);
            GivenValidPath();

            var newSeries = new Series
            {
                TvdbId = 1005,
                Title = "Super Nintendo Entertainment System",
                Path = sourcePath,
                Language = Language.English,
                AddOptions = new AddPlatformOptions
                {
                    ImportMode = "leave"
                }
            };

            var added = Subject.AddSeries(new List<Series> { newSeries });

            added.Should().ContainSingle();
            added[0].Path.Should().Be(sourcePath);
        }

        [Test]
        public void should_use_explicit_root_folder_path_when_specified_on_import()
        {
            var sourcePath = @"C:\Downloads\Super Nintendo";
            var targetRoot = @"D:\Games\Nintendo";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(sourcePath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(targetRoot))
                  .Returns(true);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(list => list);

            GivenValidSeries(1005);
            GivenValidPath();

            var newSeries = new Series
            {
                TvdbId = 1005,
                Title = "Super Nintendo Entertainment System",
                Path = sourcePath,
                RootFolderPath = targetRoot,
                Language = Language.English
            };

            var added = Subject.AddSeries(new List<Series> { newSeries });

            added.Should().ContainSingle();
            added[0].Path.Should().Be(Path.Combine(targetRoot, _fakeSeries.Title));
            added[0].RootFolderPath.Should().Be(targetRoot);
        }

        [Test]
        public void should_create_platform_bios_and_monitored_region_folders_with_normalized_separators()
        {
            var rootFolder = @"C:\Test\ROMs".AsOsAgnostic();
            var expectedPlatformPath = Path.Combine(rootFolder, "Nintendo", "Nintendo Entertainment System");

            _fakeSeries.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Title = "USA", Monitored = true },
                new Season { SeasonNumber = 2, Title = "Europe", Monitored = false }
            };

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), null))
                  .Returns("Nintendo/Nintendo Entertainment System");

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeasonFolder(It.IsAny<Series>(), 1, null))
                  .Returns("USA");

            Mocker.GetMock<IAddPlatformValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult());

            GivenValidSeries(1006);

            var newSeries = new Series
            {
                TvdbId = 1006,
                RootFolderPath = rootFolder,
                Language = Language.English
            };

            var series = Subject.AddSeries(newSeries);

            series.Path.Should().Be(expectedPlatformPath);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(expectedPlatformPath), Times.Once());
            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(Path.Combine(expectedPlatformPath, "BIOS")), Times.Once());
            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(Path.Combine(expectedPlatformPath, "USA")), Times.Once());
            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(Path.Combine(expectedPlatformPath, "Europe")), Times.Never());
        }

        [Test]
        public void should_populate_library_information_before_importing_files_for_new_platform()
        {
            var sourcePath = @"C:\Downloads\Super Nintendo".AsOsAgnostic();
            var targetRoot = @"C:\Roms".AsOsAgnostic();
            var callOrder = new List<string>();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(sourcePath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(targetRoot))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.GetFiles(sourcePath, true))
                  .Callback(() => callOrder.Add("ImportIncomingFiles"))
                  .Returns(Array.Empty<string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(r => r.All())
                  .Returns(new List<RootFolder> { new RootFolder { Id = 1, Path = targetRoot } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Callback<List<Series>>(list =>
                  {
                      callOrder.Add("AddSeries");
                      foreach (var item in list)
                      {
                          item.Id = 42;
                      }
                  })
                  .Returns<List<Series>>(list => list);

            Mocker.GetMock<IRefreshGameService>()
                  .Setup(r => r.RefreshEpisodeInfo(It.IsAny<Series>(), It.IsAny<List<Episode>>()))
                  .Callback(() => callOrder.Add("RefreshEpisodeInfo"));

            GivenValidSeries(1005);
            GivenValidPath();

            var newSeries = new Series
            {
                TvdbId = 1005,
                Title = "Super Nintendo Entertainment System",
                Path = sourcePath,
                Language = Language.English
            };

            var added = Subject.AddSeries(new List<Series> { newSeries });

            added.Should().ContainSingle();
            callOrder.Should().ContainInOrder("AddSeries", "RefreshEpisodeInfo", "ImportIncomingFiles");

            Mocker.GetMock<IRefreshGameService>()
                  .Verify(r => r.RefreshEpisodeInfo(It.Is<Series>(s => s.Id == 42), It.IsAny<List<Episode>>()), Times.Once());
        }
    }
}
