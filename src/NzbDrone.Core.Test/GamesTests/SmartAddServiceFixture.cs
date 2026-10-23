using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class SmartAddServiceFixture : CoreTest<SmartAddService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPlatformCatalogService>()
                  .Setup(s => s.GetAll())
                  .Returns(new PlatformCatalogService().GetAll());

            Mocker.GetMock<IGamePlatformRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<GamePlatform>());

            Mocker.GetMock<IGamePlatformRepository>()
                  .Setup(s => s.Insert(It.IsAny<GamePlatform>()))
                  .Returns<GamePlatform>(p =>
                  {
                      p.Id = 10;
                      return p;
                  });

            Mocker.GetMock<IGameTitleRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<GameTitle>());

            Mocker.GetMock<IGameTitleRepository>()
                  .Setup(s => s.Insert(It.IsAny<GameTitle>()))
                  .Returns<GameTitle>(t =>
                  {
                      t.Id = 99;
                      return t;
                  });
        }

        [Test]
        public void Should_auto_provision_parent_console_and_mark_game_as_wanted()
        {
            var root = Path.Combine("Library", "ROMs");
            var result = Subject.SmartAddGame(new SmartAddRequest
            {
                Title = "Super Metroid (USA) [!].sfc",
                PlatformSlug = "snes",
                RootFolderPath = root,
                IgdbId = 1025
            });

            result.Accepted.Should().BeTrue();
            result.PlatformAutoProvisioned.Should().BeTrue();
            result.Platform.Should().NotBeNull();
            result.Platform.Name.Should().Be("Super Nintendo Entertainment System");
            result.Platform.Path.Should().Be(Path.Combine(root, "Nintendo", "Super Nintendo"));
            result.Platform.Monitored.Should().BeTrue();

            result.GameTitle.Should().NotBeNull();
            result.GameTitle.Title.Should().Be("Super Metroid");
            result.GameTitle.PlatformId.Should().Be(10);
            result.GameTitle.Monitored.Should().BeTrue();
        }

        [Test]
        public void Should_reject_rom_hack_and_translation_patch_by_default()
        {
            var hackResult = Subject.SmartAddGame(new SmartAddRequest
            {
                Title = "Super Mario World Kaizo Hack (USA).sfc",
                PlatformSlug = "snes"
            });

            hackResult.Accepted.Should().BeFalse();

            var patchResult = Subject.SmartAddGame(new SmartAddRequest
            {
                Title = "Mother 3 (Japan) [T+Eng].gba",
                PlatformSlug = "gba"
            });

            patchResult.Accepted.Should().BeFalse();
        }

        [Test]
        public void Should_enforce_strict_cryptographic_hash_gate_for_unofficial_content()
        {
            const string verifiedSha256 = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

            Mocker.GetMock<IDatCacheService>()
                  .Setup(s => s.FindBySha256(verifiedSha256))
                  .Returns(new DatEntry { Sha256 = verifiedSha256, GameName = "Mother 3 English" });

            var unverifiedAttempt = Subject.SmartAddGame(new SmartAddRequest
            {
                Title = "Mother 3 (Japan) [T+Eng].gba",
                PlatformSlug = "gba",
                AllowUnofficial = true,
                ExpectedHash = "unverified_hash"
            });

            unverifiedAttempt.Accepted.Should().BeFalse();

            var verifiedAttempt = Subject.SmartAddGame(new SmartAddRequest
            {
                Title = "Mother 3 (Japan) [T+Eng].gba",
                PlatformSlug = "gba",
                AllowUnofficial = true,
                ExpectedHash = verifiedSha256
            });

            verifiedAttempt.Accepted.Should().BeTrue();
        }
    }
}
