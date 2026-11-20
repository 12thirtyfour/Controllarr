using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    [TestFixture]
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPlatformInfo>().SetupGet(c => c.Version).Returns(new Version("10.0.0"));

            var releases = new List<GitHubReleaseResource>
            {
                new GitHubReleaseResource
                {
                    TagName = "v5.0.0.123",
                    Name = "5.0.0.123",
                    Body = "* Added BIOS Files view under Library\n* Fixed region folder naming",
                    Draft = false,
                    Prerelease = true,
                    PublishedAt = new DateTime(2026, 10, 24, 0, 58, 13, DateTimeKind.Utc),
                    Assets = BuildAssetsForRelease("5.0.0.123", "develop")
                },
                new GitHubReleaseResource
                {
                    TagName = "v5.0.0.122",
                    Name = "5.0.0.122",
                    Body = "* Added console themes\n* Fixed 1G1R filtering",
                    Draft = false,
                    Prerelease = true,
                    PublishedAt = new DateTime(2026, 10, 24, 0, 25, 52, DateTimeKind.Utc),
                    Assets = BuildAssetsForRelease("5.0.0.122", "develop")
                }
            };

            var releasesJson = releases.ToJson();

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<List<GitHubReleaseResource>>(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse<List<GitHubReleaseResource>>(
                      new HttpResponse(r, new HttpHeader(), releasesJson)));
        }

        private static List<GitHubReleaseAssetResource> BuildAssetsForRelease(string version, string branch)
        {
            var runtimes = new[]
            {
                ("win-x64", ".zip"),
                ("win-x86", ".zip"),
                ("linux-x64", ".tar.gz"),
                ("linux-arm64", ".tar.gz"),
                ("linux-arm", ".tar.gz"),
                ("linux-musl-x64", ".tar.gz"),
                ("linux-musl-arm64", ".tar.gz"),
                ("osx-x64", ".tar.gz"),
                ("osx-arm64", ".tar.gz"),
                ("freebsd-x64", ".tar.gz")
            };

            return runtimes.Select(r => new GitHubReleaseAssetResource
            {
                Name = $"Controllarr.{branch}.{version}.{r.Item1}{r.Item2}",
                BrowserDownloadUrl = $"https://github.com/12thirtyfour/Controllarr/releases/download/v{version}/Controllarr.{branch}.{version}.{r.Item1}{r.Item2}",
                Digest = "sha256:7bbe01c825da6294d07722ff7f88bed6a53f61566ee9114a982d688f98ca32dc"
            }).ToList();
        }

        [Test]
        public void no_update_when_version_higher()
        {
            Subject.GetLatestUpdate("develop", new Version(10, 0)).Should().BeNull();
        }

        [Test]
        public void finds_update_when_version_lower()
        {
            var update = Subject.GetLatestUpdate("develop", new Version(5, 0, 0, 100));
            update.Should().NotBeNull();
            update.Version.Should().Be(new Version(5, 0, 0, 123));
            update.Url.Should().StartWith("https://github.com/12thirtyfour/Controllarr/releases/download/");
        }

        [Test]
        public void should_get_latest_if_branch_doesnt_exist()
        {
            Subject.GetLatestUpdate("invalid_branch", new Version(5, 0, 0, 100)).Should().NotBeNull();
        }

        [Test]
        public void should_get_recent_updates()
        {
            const string branch = "develop";
            var recent = Subject.GetRecentUpdates(branch, new Version(5, 0, 0, 100), null);

            recent.Should().NotBeEmpty();
            recent.Should().OnlyContain(c => c.Hash.IsNotNullOrWhiteSpace());
            recent.Should().OnlyContain(c => c.FileName.Contains($"Controllarr.{c.Branch}.5.0.0."));
            recent.Should().OnlyContain(c => c.ReleaseDate.Year >= 2024);
            recent.Where(c => c.Changes != null).Should().OnlyContain(c => c.Changes.New != null);
            recent.Where(c => c.Changes != null).Should().OnlyContain(c => c.Changes.Fixed != null);
        }
    }
}
