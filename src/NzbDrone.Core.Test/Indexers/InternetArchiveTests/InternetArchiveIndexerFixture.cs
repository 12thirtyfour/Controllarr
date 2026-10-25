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
            release.RequiresCredentials.Should().BeFalse();
        }

        [Test]
        public async Task should_mark_requires_credentials_true_when_doc_is_access_restricted()
        {
            var jsonResponse = @"{
                ""response"": {
                    ""numFound"": 1,
                    ""start"": 0,
                    ""docs"": [
                        {
                            ""identifier"": ""restricted_rom"",
                            ""title"": ""Restricted ROM"",
                            ""access-restricted-item"": ""true"",
                            ""item_size"": 1024
                        }
                    ]
                }
            }";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), jsonResponse));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);
            releases.First().RequiresCredentials.Should().BeTrue();
        }

        [Test]
        public async Task should_mark_requires_credentials_true_when_indexer_setting_is_enabled()
        {
            var settings = (InternetArchiveIndexerSettings)Subject.Definition.Settings;
            settings.RequireCredentials = true;

            var jsonResponse = @"{
                ""response"": {
                    ""numFound"": 1,
                    ""start"": 0,
                    ""docs"": [
                        {
                            ""identifier"": ""public_rom"",
                            ""title"": ""Public ROM"",
                            ""item_size"": 1024
                        }
                    ]
                }
            }";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), jsonResponse));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);
            releases.First().RequiresCredentials.Should().BeTrue();
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
        public void should_generate_search_request_with_primary_archives_and_fallback()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/htgdb-gamepacks" }
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var criteria = new SingleEpisodeSearchCriteria
            {
                SceneTitles = new List<string> { "Super Mario World" }
            };

            var requests = generator.GetSearchRequests(criteria).GetAllTiers().SelectMany(x => x).ToList();

            requests.Should().HaveCount(2);

            // Tier 1: Query primary archive metadata directly for single ROM files
            requests[0].HttpRequest.Url.FullUri.Should().Contain("/metadata/htgdb-gamepacks/files?search=Super%20Mario%20World");

            // Tier 2: Fallback query across collection
            requests[1].HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            requests[1].HttpRequest.Url.FullUri.Should().Contain("collection%3Ahtgdb-gamepacks");
            requests[1].HttpRequest.Url.FullUri.Should().Contain("Super%20Mario%20World");
        }

        [Test]
        public void should_generate_search_request_without_primary_archives()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = Array.Empty<string>()
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var criteria = new SingleEpisodeSearchCriteria
            {
                SceneTitles = new List<string> { "Super Mario World" }
            };

            var requests = generator.GetSearchRequests(criteria).GetAllTiers().SelectMany(x => x).ToList();

            requests.Should().HaveCount(1);
            requests[0].HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            requests[0].HttpRequest.Url.FullUri.Should().Contain("Super%20Mario%20World");
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

        [TestCase("https://archive.org/details/htgdb-gamepacks", "htgdb-gamepacks")]
        [TestCase("https://archive.org/details/htgdb-gamepacks/", "htgdb-gamepacks")]
        [TestCase("https://archive.org/download/htgdb-gamepacks", "htgdb-gamepacks")]
        [TestCase("htgdb-gamepacks", "htgdb-gamepacks")]
        [TestCase("https://archive.org/details/no-intro-rom-sets/item", "no-intro-rom-sets")]
        public void should_extract_identifier_from_archive_urls(string inputUrl, string expectedId)
        {
            InternetArchiveIndexerSettings.ExtractIdentifier(inputUrl).Should().Be(expectedId);
        }

        [Test]
        public void should_parse_metadata_files_response_for_primary_archive()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/htgdb-gamepacks" }
            };

            var parser = new InternetArchiveParser(settings);
            var metadataJson = @"{
                ""result"": [
                    { ""name"": ""@ = MiSTer Ready.txt"", ""size"": ""181"", ""format"": ""Text"" },
                    { ""name"": ""Super Mario World (USA).sfc"", ""size"": ""524288"", ""format"": ""ROM"", ""mtime"": 1600000000, ""private"": ""false"" },
                    { ""name"": ""Chrono Trigger (USA).sfc"", ""size"": ""4194304"", ""format"": ""ROM"", ""mtime"": 1600000000, ""private"": ""true"" }
                ]
            }";

            var request = new HttpRequest("https://archive.org/metadata/htgdb-gamepacks/files?search=Super%20Mario%20World");
            var response = new HttpResponse(request, new HttpHeader(), metadataJson);
            var indexerResponse = new IndexerResponse(new IndexerRequest(request), response);

            var releases = parser.ParseResponse(indexerResponse);

            releases.Should().HaveCount(1);
            var release = releases.First();
            release.Title.Should().Be("Super Mario World (USA).sfc");
            release.DownloadUrl.Should().Be("https://archive.org/download/htgdb-gamepacks/Super%20Mario%20World%20%28USA%29.sfc");
            release.InfoUrl.Should().Be("https://archive.org/details/htgdb-gamepacks");
            release.Size.Should().Be(524288);
            release.RequiresCredentials.Should().BeFalse();
        }
    }
}
