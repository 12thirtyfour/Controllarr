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
using NzbDrone.Core.Tv;

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

            requests.Should().HaveCount(3);
            requests[0].HttpRequest.Url.FullUri.Should().Be("https://archive.org/metadata/no-intro_romsets/files");
            requests[1].HttpRequest.Url.FullUri.Should().Be("https://archive.org/metadata/redump/files");

            var request = requests[2];
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

            requests.Should().HaveCount(3);

            // Tier 1: Query primary archive metadata directly for single ROM files
            requests[0].HttpRequest.Url.FullUri.Should().Contain("/metadata/htgdb-gamepacks/files?search=Super%20Mario%20World");

            // Tier 2: Fallback query across collection
            requests[1].HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            requests[1].HttpRequest.Url.FullUri.Should().Contain("collection%3Ahtgdb-gamepacks");
            requests[1].HttpRequest.Url.FullUri.Should().Contain("Super%20Mario%20World");

            // Tier 3: Broad search across Internet Archive software library
            requests[2].HttpRequest.Url.FullUri.Should().Contain("advancedsearch.php");
            requests[2].HttpRequest.Url.FullUri.Should().Contain("mediatype%3Asoftware");
            requests[2].HttpRequest.Url.FullUri.Should().Contain("Super%20Mario%20World");
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

        [Test]
        public void should_parse_search_response_when_title_is_array_of_strings()
        {
            var parser = new InternetArchiveParser(new InternetArchiveIndexerSettings());
            var json = @"{
                ""response"": {
                    ""numFound"": 1,
                    ""start"": 0,
                    ""docs"": [
                        {
                            ""identifier"": ""2003_11_27_DPCS0803"",
                            ""title"": [""2003-11-27 DPCS0803"", ""2003 11 27 DPCS0803""],
                            ""mediatype"": [""software""],
                            ""date"": [""2003-11-27""],
                            ""publicdate"": ""2020-01-01T00:00:00Z"",
                            ""item_size"": 123456
                        }
                    ]
                }
            }";

            var request = new HttpRequest("https://archive.org/advancedsearch.php?q=test");
            var response = new HttpResponse(request, new HttpHeader(), json);
            var indexerResponse = new IndexerResponse(new IndexerRequest(request), response);

            var releases = parser.ParseResponse(indexerResponse);

            releases.Should().HaveCount(1);
            releases[0].Title.Should().Be("2003-11-27 DPCS0803");
            releases[0].Guid.Should().Be("IA-2003_11_27_DPCS0803");
            releases[0].Size.Should().Be(123456);
        }

        [Test]
        public void should_scope_query_strictly_to_primary_archives_when_configured()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/htgdb-gamepacks" }
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var criteria = new SingleEpisodeSearchCriteria
            {
                Series = new Series { Title = "Nintendo Entertainment System", TitleSlug = "nes" },
                Episodes = new List<Episode> { new Episode { Title = "106 Games" } }
            };

            var requests = generator.GetSearchRequests(criteria).GetAllTiers().SelectMany(x => x).ToList();
            var pagedRequest = requests.FirstOrDefault(r => r.HttpRequest.Url.FullUri.Contains("advancedsearch.php"));

            pagedRequest.Should().NotBeNull();
            var uri = pagedRequest.HttpRequest.Url.FullUri;
            uri.Should().Contain("htgdb-gamepacks");
            uri.Should().NotContain("OR+mediatype%3Asoftware");
        }

        [Test]
        public void should_pass_test_connection_when_probe_is_successful()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), "{}"));

            var failures = Subject.Test();

            failures.IsValid.Should().BeTrue();
            failures.Errors.Should().BeEmpty();
        }

        [Test]
        public void should_fail_test_connection_when_auth_error_returned()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), new byte[0], System.Net.HttpStatusCode.Unauthorized));

            var failures = Subject.Test();

            failures.IsValid.Should().BeFalse();
            failures.Errors.Should().HaveCount(1);
            failures.Errors.First().ErrorMessage.Should().Contain("authentication failed");
        }

        [Test]
        public void should_parse_metadata_files_response_for_primary_archive_with_platform_hash_parameter()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/chd_segacd" }
            };

            var parser = new InternetArchiveParser(settings);
            var metadataJson = @"{
                ""result"": [
                    { ""name"": ""@ = Sega CD Directory.txt"", ""size"": ""181"", ""format"": ""Text"" },
                    { ""name"": ""Sonic CD (USA).chd"", ""size"": ""471859200"", ""format"": ""CHD"", ""mtime"": 1600000000, ""private"": ""false"" },
                    { ""name"": ""Sonic The Hedgehog CD (USA).chd"", ""size"": ""471859200"", ""format"": ""CHD"", ""mtime"": 1600000000, ""private"": ""false"" },
                    { ""name"": ""Sonic_CD_(Japan).chd"", ""size"": ""460000000"", ""format"": ""CHD"", ""mtime"": 1600000000, ""private"": ""false"" },
                    { ""name"": ""Snatcher (USA).chd"", ""size"": ""524288000"", ""format"": ""CHD"", ""mtime"": 1600000000, ""private"": ""false"" }
                ]
            }";

            var request = new HttpRequest("https://archive.org/metadata/chd_segacd/files?search=Sonic%20CD#_platform=segacd&_platformName=Sega%20CD");
            var response = new HttpResponse(request, new HttpHeader(), metadataJson);
            var indexerResponse = new IndexerResponse(new IndexerRequest(request), response);

            var releases = parser.ParseResponse(indexerResponse);

            releases.Should().HaveCount(3);
            releases.Should().Contain(r => r.Title == "Sonic CD (USA).chd" && r.DownloadUrl == "https://archive.org/download/chd_segacd/Sonic%20CD%20%28USA%29.chd");
            releases.Should().Contain(r => r.Title == "Sonic The Hedgehog CD (USA).chd");
            releases.Should().Contain(r => r.Title == "Sonic_CD_(Japan).chd");
            releases.Should().NotContain(r => r.Title.Contains("Snatcher"));
            releases.First().Size.Should().Be(471859200);
        }

        [Test]
        public void should_exclude_primary_archive_collection_itself_and_huge_archives_from_search_results()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/chd_segacd" }
            };

            var parser = new InternetArchiveParser(settings);
            var searchJson = @"{
                ""response"": {
                    ""numFound"": 2,
                    ""start"": 0,
                    ""docs"": [
                        {
                            ""identifier"": ""chd_segacd"",
                            ""title"": ""Sega CD Complete CHD Collection"",
                            ""mediatype"": ""software"",
                            ""item_size"": 506806149120
                        },
                        {
                            ""identifier"": ""sonic_cd_standalone_release"",
                            ""title"": ""Sonic CD (USA)"",
                            ""mediatype"": ""software"",
                            ""item_size"": 471859200
                        }
                    ]
                }
            }";

            var request = new HttpRequest("https://archive.org/advancedsearch.php?q=Sonic%20CD#_platform=segacd");
            var response = new HttpResponse(request, new HttpHeader(), searchJson);
            var indexerResponse = new IndexerResponse(new IndexerRequest(request), response);

            var releases = parser.ParseResponse(indexerResponse);

            releases.Should().HaveCount(1);
            releases[0].Guid.Should().Be("IA-sonic_cd_standalone_release");
            releases[0].Title.Should().Be("Sonic CD (USA)");
            releases[0].Size.Should().Be(471859200);
        }

        [Test]
        public void should_generate_single_collection_request_for_primary_archives_without_search_query()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/nointro.nes" }
            };

            var generator = new InternetArchiveRequestGenerator { Settings = settings };
            var criteria = new SeasonSearchCriteria
            {
                Series = new Series { Title = "Nintendo Entertainment System", TitleSlug = "nes" },
                IsPlatformCollectionSearch = true,
                SceneTitles = new List<string> { "NES", "Nintendo Entertainment System", "NES romset" }
            };

            var requests = generator.GetSearchRequests(criteria).GetAllTiers().SelectMany(x => x).ToList();

            // Tier 1 should have exactly one request for the primary archive without ?search= query
            var tier1 = requests.First();
            tier1.HttpRequest.Url.FullUri.Should().Be("https://archive.org/metadata/nointro.nes/files#_platform=nes&_platformName=Nintendo%20Entertainment%20System&_collection=true");
        }

        [Test]
        public void should_parse_collection_zip_file_as_season_pack_from_primary_archive()
        {
            var settings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/nointro.nes" }
            };

            var parser = new InternetArchiveParser(settings);
            var metadataJson = @"{
                ""result"": [
                    { ""name"": ""Nintendo - Nintendo Entertainment System.zip"", ""size"": ""123456789"", ""format"": ""ZIP"" },
                    { ""name"": ""readme.txt"", ""size"": ""100"", ""format"": ""Text"" },
                    { ""name"": ""Super Mario Bros (USA).nes"", ""size"": ""40960"", ""format"": ""ROM"" }
                ]
            }";

            var request = new HttpRequest("https://archive.org/metadata/nointro.nes/files#_platform=nes&_platformName=Nintendo%20Entertainment%20System&_collection=true");
            var response = new HttpResponse(request, new HttpHeader(), metadataJson);
            var indexerResponse = new IndexerResponse(new IndexerRequest(request), response);

            var releases = parser.ParseResponse(indexerResponse);

            releases.Should().HaveCount(1);
            var release = releases.First();
            release.Title.Should().Be("Nintendo - Nintendo Entertainment System.zip");
            release.DownloadUrl.Should().Be("https://archive.org/download/nointro.nes/Nintendo%20-%20Nintendo%20Entertainment%20System.zip");
            release.ParsedRomInfo.Should().NotBeNull();
            release.ParsedRomInfo.IsFullSetArchive.Should().BeTrue();
        }
    }
}
