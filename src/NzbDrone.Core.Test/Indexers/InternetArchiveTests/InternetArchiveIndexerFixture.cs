using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Indexers.InternetArchiveTests
{
    [TestFixture]
    public class InternetArchiveIndexerFixture : CoreTest<InternetArchiveIndexer>
    {
        [SetUp]
        public void Setup()
        {
            Subject.Definition = new IndexerDefinition
            {
                Name = "InternetArchive",
                Settings = new InternetArchiveIndexerSettings
                {
                    BaseUrl = "https://archive.org",
                    Collections = new[] { "no-intro_romsets", "redump", "softwarelibrary" }
                }
            };
        }

        [Test]
        public async Task should_parse_recent_feed_from_InternetArchive()
        {
            var jsonResponse = @"{
                ""responseHeader"": { ""status"": 0, ""QTime"": 10 },
                ""response"": {
                    ""numFound"": 1,
                    ""start"": 0,
                    ""docs"": [
                        {
                            ""identifier"": ""snes_no_intro_romset"",
                            ""title"": ""Super Nintendo Entertainment System (No-Intro)"",
                            ""mediatype"": ""software"",
                            ""collection"": [""no-intro_romsets""],
                            ""date"": ""2021-01-01T00:00:00Z"",
                            ""publicdate"": ""2021-01-01T00:00:00Z"",
                            ""item_size"": 104857600,
                            ""downloads"": 500
                        }
                    ]
                }
            }";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), jsonResponse));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);
            var release = releases.First();

            release.Guid.Should().Be("IA-snes_no_intro_romset");
            release.Title.Should().Be("Super Nintendo Entertainment System (No-Intro)");
            release.DownloadProtocol.Should().Be(DownloadProtocol.Http);
            release.DownloadUrl.Should().Be("https://archive.org/compress/snes_no_intro_romset");
            release.InfoUrl.Should().Be("https://archive.org/details/snes_no_intro_romset");
            release.Size.Should().Be(104857600);
            release.PublishDate.Should().Be(DateTime.Parse("2021-01-01T00:00:00Z").ToUniversalTime());
        }

        [Test]
        public void should_generate_recent_request_with_collections()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                Collections = new[] { "no-intro_romsets", "redump" }
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var requests = generator.GetRecentRequests().GetAllTiers().SelectMany(x => x).ToList();

            requests.Should().HaveCount(1);
            var request = requests.First();

            request.HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            request.HttpRequest.Url.FullUri.Should().Contain("collection%3Ano-intro_romsets");
            request.HttpRequest.Url.FullUri.Should().Contain("collection%3Aredump");
            request.HttpRequest.Url.FullUri.Should().Contain("output=json");
        }

        [Test]
        public void should_generate_search_request_with_term()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                Collections = new[] { "no-intro_romsets" }
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var criteria = new SingleEpisodeSearchCriteria
            {
                SceneTitles = new List<string> { "Super Mario World" }
            };

            var requests = generator.GetSearchRequests(criteria).GetAllTiers().SelectMany(x => x).ToList();

            requests.Should().HaveCount(1);
            var request = requests.First();

            request.HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            request.HttpRequest.Url.FullUri.Should().Contain("collection%3Ano-intro_romsets");
            request.HttpRequest.Url.FullUri.Should().Contain("Super%20Mario%20World");
        }

        [Test]
        public async Task should_handle_empty_response_gracefully()
        {
            var jsonResponse = @"{ ""response"": { ""numFound"": 0, ""start"": 0, ""docs"": [] } }";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), jsonResponse));

            var releases = await Subject.FetchRecent();

            releases.Should().BeEmpty();
        }

        [Test]
        public void should_validate_default_settings()
        {
            var settings = new InternetArchiveIndexerSettings();
            var validationResult = settings.Validate();

            validationResult.IsValid.Should().BeTrue();
        }
    }
}
