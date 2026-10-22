using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Games;
using Sonarr.Api.V3.Games.Model;
using Sonarr.Http;

namespace NzbDrone.Api.Test.Games
{
    [TestFixture]
    public class GamePlatformControllerFixture : TestBase<GamePlatformController>
    {
        [Test]
        public void should_have_v3_api_controller_attribute()
        {
            var attributes = typeof(GamePlatformController).GetCustomAttributes(typeof(V3ApiControllerAttribute), true);
            attributes.Should().HaveCount(1);

            var attr = (V3ApiControllerAttribute)attributes.First();
            attr.Template.Should().Be("api/v3/gameplatform");
        }

        [Test]
        public void should_get_all_platforms()
        {
            var platforms = new List<GamePlatform>
            {
                new GamePlatform
                {
                    Id = 1,
                    Name = "Super Nintendo",
                    Slug = "snes",
                    IgdbId = 19,
                    Overview = "16-bit console",
                    Path = "/games/snes",
                    Monitored = true,
                    Code = "SNES",
                    ReleaseYear = 1990
                }
            };

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.All())
                .Returns(platforms);

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
            result[0].Name.Should().Be("Super Nintendo");
            result[0].Slug.Should().Be("snes");
            result[0].IgdbId.Should().Be(19);
            result[0].Code.Should().Be("SNES");
            result[0].ReleaseYear.Should().Be(1990);
        }

        [Test]
        public void should_get_platform_by_id()
        {
            var platform = new GamePlatform
            {
                Id = 1,
                Name = "Super Nintendo",
                Slug = "snes",
                IgdbId = 19
            };

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.Get(1))
                .Returns(platform);

            var result = Subject.GetResourceByIdWithErrorHandler(1);

            result.Result.Should().NotBeNull();
            Mocker.GetMock<IGamePlatformRepository>().Verify(r => r.Get(1), Times.Once());
        }

        [Test]
        public void should_create_platform()
        {
            var resource = new GamePlatformResource
            {
                Name = "Game Boy",
                Slug = "gb",
                IgdbId = 33
            };

            var insertedPlatform = new GamePlatform
            {
                Id = 2,
                Name = "Game Boy",
                Slug = "gb",
                IgdbId = 33
            };

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.Insert(It.Is<GamePlatform>(p => p.Name == "Game Boy")))
                .Returns(insertedPlatform);

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.Get(2))
                .Returns(insertedPlatform);

            var actionResult = Subject.Create(resource);

            actionResult.Should().NotBeNull();
            Mocker.GetMock<IGamePlatformRepository>().Verify(r => r.Insert(It.Is<GamePlatform>(p => p.Name == "Game Boy")), Times.Once());
        }

        [Test]
        public void should_update_platform()
        {
            var resource = new GamePlatformResource
            {
                Id = 1,
                Name = "Super Nintendo Entertainment System",
                Slug = "snes"
            };

            var updatedPlatform = new GamePlatform
            {
                Id = 1,
                Name = "Super Nintendo Entertainment System",
                Slug = "snes"
            };

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.Update(It.IsAny<GamePlatform>()))
                .Returns(updatedPlatform);

            Mocker.GetMock<IGamePlatformRepository>()
                .Setup(r => r.Get(1))
                .Returns(updatedPlatform);

            var actionResult = Subject.Update(resource);

            actionResult.Should().NotBeNull();
            Mocker.GetMock<IGamePlatformRepository>().Verify(r => r.Update(It.Is<GamePlatform>(p => p.Id == 1 && p.Name == "Super Nintendo Entertainment System")), Times.Once());
        }

        [Test]
        public void should_delete_platform()
        {
            Subject.DeletePlatform(1);

            Mocker.GetMock<IGamePlatformRepository>().Verify(r => r.Delete(1), Times.Once());
        }
    }
}
