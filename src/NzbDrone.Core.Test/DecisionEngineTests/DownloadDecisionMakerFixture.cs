using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class DownloadDecisionMakerFixture : CoreTest<DownloadDecisionMaker>
    {
        private List<ReleaseInfo> _reports;
        private RemoteEpisode _remoteEpisode;

        private Mock<IDownloadDecisionEngineSpecification> _pass1;
        private Mock<IDownloadDecisionEngineSpecification> _pass2;
        private Mock<IDownloadDecisionEngineSpecification> _pass3;

        private Mock<IDownloadDecisionEngineSpecification> _fail1;
        private Mock<IDownloadDecisionEngineSpecification> _fail2;
        private Mock<IDownloadDecisionEngineSpecification> _fail3;

        private Mock<IDownloadDecisionEngineSpecification> _failDelayed1;

        [SetUp]
        public void Setup()
        {
            _pass1 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass2 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass3 = new Mock<IDownloadDecisionEngineSpecification>();

            _fail1 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail2 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail3 = new Mock<IDownloadDecisionEngineSpecification>();

            _failDelayed1 = new Mock<IDownloadDecisionEngineSpecification>();

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail3"));

            _failDelayed1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "failDelayed1"));
            _failDelayed1.SetupGet(c => c.Priority).Returns(SpecificationPriority.Disk);

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "Super.Mario.64.USA.N64-DUMP" } };
            _remoteEpisode = new RemoteEpisode
            {
                Series = new Series(),
                Episodes = new List<Episode> { new Episode() }
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                  .Returns(_remoteEpisode);
        }

        private void GivenSpecifications(params Mock<IDownloadDecisionEngineSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(mocks.Select(c => c.Object));
        }

        [Test]
        public void should_call_all_specifications()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetRssDecision(_reports).ToList();

            _fail1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_call_delayed_specifications_if_non_delayed_passed()
        {
            GivenSpecifications(_pass1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();
            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_not_call_delayed_specifications_if_non_delayed_failed()
        {
            GivenSpecifications(_fail1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();

            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_return_rejected_if_single_specs_fail()
        {
            GivenSpecifications(_fail1);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_of_specs_fail()
        {
            GivenSpecifications(_pass1, _fail1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_pass_if_all_specs_pass()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
        }

        [Test]
        public void should_have_same_number_of_rejections_as_specs_that_failed()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            var result = Subject.GetRssDecision(_reports);
            result.Single().Rejections.Should().HaveCount(3);
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_not_parsable()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            // Stripping the metadata leaves brackets rather than a title, so neither parser
            // recovers anything to map.
            _reports[0].Title = "[Not][parsable][1080p][x265(10bit)]";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_map_a_title_the_regexes_cannot_parse()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "Not parsable";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Once());
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_series_title_is_blank()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "S19E37 - Snow White and the Seven Dwarves";

            var results = Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());

            results.Should().BeEmpty();
        }

        [Test]
        public void should_return_rejected_result_for_unparsable_search()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "S19E37 - Snow White and the Seven Dwarves";

            Subject.GetSearchDecision(_reports, new SingleEpisodeSearchCriteria()).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_make_decision_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            Subject.GetRssDecision(_reports);

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void broken_report_shouldnt_blowup_the_process()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Reset();

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Super.Mario.64.USA.N64-DUMP" },
                    new ReleaseInfo { Title = "Super.Mario.64.USA.N64-DUMP" },
                    new ReleaseInfo { Title = "Super.Mario.64.USA.N64-DUMP" }
                };

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Exactly(_reports.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_only_include_reports_for_requested_episodes()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.Title = "Nintendo Entertainment System")
                                        .With(s => s.TitleSlug = "nes")
                                        .Build();

            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeriesId = series.Id, Series = series, Title = "Super Mario Bros", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeNumber = 1, SceneEpisodeNumber = 1 },
                new Episode { Id = 2, SeriesId = series.Id, Series = series, Title = "The Legend of Zelda", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeNumber = 2, SceneEpisodeNumber = 2 }
            };

            var criteria = new SeasonSearchCriteria { Series = series, Episodes = episodes.Take(1).ToList(), SeasonNumber = 1 };

            var reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "Super.Mario.Bros.NES-DUMP" },
                new ReleaseInfo { Title = "The.Legend.of.Zelda.NES-DUMP" }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = p.EpisodeNumbers?.Any() == true ? episodes.Where(v => v.SceneEpisodeNumber == p.EpisodeNumbers.First()).ToList() : new List<Episode>()
                    });

            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(new List<IDownloadDecisionEngineSpecification>
            {
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.Search.GameRequestedSpecification>()
            });

            var decisions = Subject.GetSearchDecision(reports, criteria);

            var approvedDecisions = decisions.Where(v => v.Approved).ToList();

            approvedDecisions.Count.Should().Be(1);
        }

        [Test]
        public void should_not_allow_download_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteEpisode.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_not_allow_download_if_no_episodes_found()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Episodes = new List<Episode>();

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteEpisode.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Reset();

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Super.Mario.64.USA.N64-DUMP" },
                };

            Subject.GetRssDecision(_reports).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_title_is_an_alias_for_another_series()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
            result.First().Rejections.First().Reason.Should().Be(DownloadRejectionReason.UnknownPlatform);
        }

        [Test]
        public void should_skip_tv_and_video_releases()
        {
            var tvReports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                new ReleaseInfo { Title = "Breaking.Bad.S05E16.720p.HDTV.x264-ASAP.mkv" },
                new ReleaseInfo { Title = "[HorribleSubs] One Piece - 950 [1080p].mkv" },
                new ReleaseInfo { Title = "The.Office.Season.3.Complete.1080p" },
                new ReleaseInfo { Title = "Show.Title.S02.Complete.720p.HDTV" }
            };

            var decisions = Subject.GetRssDecision(tvReports).ToList();

            decisions.Should().BeEmpty();
        }

        [Test]
        public void should_match_rom_releases_quickly_against_large_episode_catalog()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.Id = 10)
                                        .With(s => s.Title = "Nintendo 64")
                                        .With(s => s.CleanTitle = "nintendo64")
                                        .With(s => s.TitleSlug = "nintendo-64")
                                        .Build();

            var episodes = new List<Episode>(5000);
            for (var i = 1; i <= 5000; i++)
            {
                episodes.Add(new Episode
                {
                    Id = i,
                    SeriesId = series.Id,
                    Series = series,
                    SeasonNumber = 1,
                    EpisodeNumber = i,
                    Title = i == 2500 ? "Super Mario 64" : i == 4200 ? "Ys" : $"Retro Adventure Title {i}"
                });
            }

            var criteria = new SeasonSearchCriteria
            {
                Series = series,
                Episodes = episodes,
                SeasonNumber = 1
            };

            var reports = new List<ReleaseInfo>(200);
            for (var i = 0; i < 197; i++)
            {
                reports.Add(new ReleaseInfo { Title = $"Retro.Adventure.Title.{(i % 200) + 1}.(USA).N64.z64" });
            }

            reports.Add(new ReleaseInfo { Title = "Super.Mario.64.(USA).N64.z64" });
            reports.Add(new ReleaseInfo { Title = "Ys.(Japan).N64.z64" });
            reports.Add(new ReleaseInfo { Title = "Super.Mario.64.(USA).PS2.iso" });

            var episodeByNumber = episodes.ToDictionary(e => e.EpisodeNumber);

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = p.EpisodeNumbers?.Select(n => episodeByNumber[n]).ToList() ?? new List<Episode>()
                    });

            GivenSpecifications(_pass1);

            var sw = Stopwatch.StartNew();
            var decisions = Subject.GetSearchDecision(reports, criteria).ToList();
            sw.Stop();

            decisions.Should().HaveCount(199);
            decisions.Count(d => d.Approved).Should().Be(199);
            decisions.Should().ContainSingle(d => d.Approved && d.RemoteEpisode.Episodes.Any(e => e.Id == 2500) && d.RemoteEpisode.Release.Title == "Super.Mario.64.(USA).N64.z64");
            decisions.Should().ContainSingle(d => d.Approved && d.RemoteEpisode.Episodes.Any(e => e.Id == 4200) && d.RemoteEpisode.Release.Title == "Ys.(Japan).N64.z64");
            decisions.Should().NotContain(d => d.RemoteEpisode.Release.Title == "Super.Mario.64.(USA).PS2.iso");
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_reject_sequels_wrong_regions_tv_rips_saves_betas_and_hacks_on_retail_single_game_search()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.Id = 44)
                                        .With(s => s.Title = "Nintendo DS")
                                        .With(s => s.CleanTitle = "nintendods")
                                        .With(s => s.TitleSlug = "nds")
                                        .Build();

            var pokemonWhite = new Episode
            {
                Id = 101,
                SeriesId = series.Id,
                Series = series,
                SeasonNumber = 1,
                EpisodeNumber = 101,
                Title = "Pokemon - White Version"
            };

            var criteria = new SingleEpisodeSearchCriteria
            {
                Series = series,
                Episodes = new List<Episode> { pokemonWhite },
                SeasonNumber = 1,
                EpisodeNumber = 101
            };

            var reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "Pokemon.White.Version.(USA).NDS-DUMP" },
                new ReleaseInfo { Title = "Pokemon.White.Version.2.(USA).NDS-DUMP" },
                new ReleaseInfo { Title = "Pokemon.White.Version.EUR.NDS-SweeTnDs" },
                new ReleaseInfo { Title = "POKEMON White eu-nl (dsi bins dumped)" },
                new ReleaseInfo { Title = "Pokemon White (PT-BR)" },
                new ReleaseInfo { Title = "Pokemon White Version Fan Translated" },
                new ReleaseInfo { Title = "Pokemon White 2 Kaizo Full Release V 1d 5" },
                new ReleaseInfo { Title = "Pokemon Diamond And Pearl Cartoon Network TVRips 2007-2008" },
                new ReleaseInfo { Title = "Pokemon White Save. 7z" },
                new ReleaseInfo { Title = "Pokemon White Version (USA) (Beta)" },
                new ReleaseInfo { Title = "Pokemon White Version Demo (Shareware)" }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = p.SeasonNumber == 1 ? new List<Episode> { pokemonWhite } : new List<Episode>()
                    });

            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(new List<IDownloadDecisionEngineSpecification>
            {
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.PlatformPackOnlySpecification>(),
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.Search.SingleGameSearchMatchSpecification>(),
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.Search.GameRequestedSpecification>()
            });

            var decisions = Subject.GetSearchDecision(reports, criteria).ToList();

            decisions.Where(d => d.Approved).Select(d => d.RemoteEpisode.Release.Title)
                .Should().BeEquivalentTo(new[] { "Pokemon.White.Version.(USA).NDS-DUMP" });
            decisions.Should().NotContain(d => d.RemoteEpisode.Release.Title.Contains("Cartoon Network TVRips"));
            decisions.Should().NotContain(d => d.RemoteEpisode.Release.Title.Contains("Save."));
        }

        [Test]
        public void should_short_circuit_database_upgrade_specs_when_parsing_tier_rejects_collection_pack()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.Id = 55)
                                        .With(s => s.Title = "Nintendo DS")
                                        .With(s => s.CleanTitle = "nintendods")
                                        .With(s => s.TitleSlug = "nds")
                                        .Build();

            var targetEp = new Episode { Id = 1, SeriesId = series.Id, Series = series, SeasonNumber = 1, EpisodeNumber = 1, Title = "Pokemon Diamond Version" };
            var criteria = new SingleEpisodeSearchCriteria
            {
                Series = series,
                Episodes = new List<Episode> { targetEp },
                SeasonNumber = 1,
                EpisodeNumber = 1
            };

            var parsingRejectSpec = new Mock<IDownloadDecisionEngineSpecification>();
            parsingRejectSpec.SetupGet(s => s.Priority).Returns(SpecificationPriority.Parsing);
            parsingRejectSpec.SetupGet(s => s.Type).Returns(RejectionType.Permanent);
            parsingRejectSpec.Setup(s => s.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()))
                .Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.FullRegion, "Collection package rejected"));

            var databaseUpgradeSpec = new Mock<IDownloadDecisionEngineSpecification>();
            databaseUpgradeSpec.SetupGet(s => s.Priority).Returns(SpecificationPriority.Database);
            databaseUpgradeSpec.SetupGet(s => s.Type).Returns(RejectionType.Permanent);

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(_remoteEpisode);

            GivenSpecifications(parsingRejectSpec, databaseUpgradeSpec);

            var decisions = Subject.GetSearchDecision(new List<ReleaseInfo> { new ReleaseInfo { Title = "Nintendo DS Full Set (No-Intro)" } }, criteria).ToList();

            decisions.Should().ContainSingle().Which.Approved.Should().BeFalse();
            databaseUpgradeSpec.Verify(s => s.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_flag_single_game_from_internet_archive_redump_collection_as_multi_region()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.Id = 77)
                                        .With(s => s.Title = "Sega CD")
                                        .With(s => s.CleanTitle = "segacd")
                                        .With(s => s.TitleSlug = "segacd")
                                        .Build();

            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeriesId = series.Id, Series = series, SeasonNumber = 1, EpisodeNumber = 1, Title = "After Burner III" },
                new Episode { Id = 2, SeriesId = series.Id, Series = series, SeasonNumber = 2, EpisodeNumber = 1, Title = "After Armageddon Gaiden" }
            };

            var reports = new List<ReleaseInfo>
            {
                new ReleaseInfo
                {
                    Title = "After Burner III (USA).7z",
                    InfoUrl = "https://archive.org/details/sega-cd-redump"
                },
                new ReleaseInfo
                {
                    Title = "Sonic Mega Collection (USA).7z",
                    InfoUrl = "https://archive.org/details/sega-cd-redump"
                },
                new ReleaseInfo
                {
                    Title = "Sega CD Redump Complete Set.7z",
                    InfoUrl = "https://archive.org/details/sega-cd-redump"
                }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = episodes
                    });

            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(new List<IDownloadDecisionEngineSpecification>
            {
                Mocker.Resolve<MultiRegionSpecification>()
            });

            var decisions = Subject.GetRssDecision(reports).ToList();

            decisions.Should().HaveCount(3);
            decisions[0].Approved.Should().BeTrue();
            decisions[0].RemoteEpisode.ParsedEpisodeInfo.IsMultiSeason.Should().BeFalse();
            decisions[1].Approved.Should().BeTrue();
            decisions[1].RemoteEpisode.ParsedEpisodeInfo.IsMultiSeason.Should().BeFalse();
            decisions[2].Approved.Should().BeFalse();
            decisions[2].RemoteEpisode.ParsedEpisodeInfo.IsMultiSeason.Should().BeTrue();
        }

        [Test]
        public void should_match_philips_cdi_dash_formatted_releases_and_reject_atari_st_and_tv_bleed()
        {
            var platform = new Series
            {
                Id = 55,
                Title = "Philips CD-i",
                CleanTitle = "cdi",
                TitleSlug = "cdi",
                Seasons = new List<Season> { new Season { SeasonNumber = 1 }, new Season { SeasonNumber = 2 } }
            };

            var game1 = new Episode { Id = 501, SeriesId = 55, SeasonNumber = 1, EpisodeNumber = 1, Title = "Escape From Cyber City" };
            var game2 = new Episode { Id = 502, SeriesId = 55, SeasonNumber = 1, EpisodeNumber = 2, Title = "How The Rhinoceros Got His Skin" };
            var game3 = new Episode { Id = 503, SeriesId = 55, SeasonNumber = 1, EpisodeNumber = 3, Title = "The Addams Family" };

            var searchCriteria = new SeasonSearchCriteria
            {
                Series = platform,
                SeasonNumber = 1,
                Episodes = new List<Episode> { game1, game2, game3 }
            };

            var reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "Escape From Cyber City - Philips CD-i - USA", Indexer = "Internet Archive" },
                new ReleaseInfo { Title = "How The Rhinoceros Got His Skin - Philips CDi - USA Release", Indexer = "Internet Archive" },
                new ReleaseInfo { Title = "The Addams Family (Atari ST)", Indexer = "Internet Archive" },
                new ReleaseInfo { Title = "The Addams Family (tv series)", Indexer = "Internet Archive" },
                new ReleaseInfo { Title = "The Addams Family (tv show)", Indexer = "Internet Archive" }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), searchCriteria))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, tvdb, rage, imdb, sc) => new RemoteEpisode
                {
                    Series = sc.Series,
                    ParsedEpisodeInfo = p,
                    Episodes = p.EpisodeNumbers?.Contains(1) == true
                        ? new List<Episode> { game1 }
                        : new List<Episode> { game2 }
                });

            GivenSpecifications(_pass1);

            var decisions = Subject.GetSearchDecision(reports, searchCriteria);

            decisions.Should().HaveCount(2);
            decisions.Should().OnlyContain(d => d.Approved);
            decisions.Select(d => d.RemoteEpisode.Episodes.Single().Title)
                .Should().BeEquivalentTo(new[] { "Escape From Cyber City", "How The Rhinoceros Got His Skin" });
        }
    }
}
