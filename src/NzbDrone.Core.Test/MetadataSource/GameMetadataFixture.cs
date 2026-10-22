using System.Collections.Generic;
using System.Threading.Tasks;
using DryIoc;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.MetadataSource.Game.Model;
using NzbDrone.Core.MetadataSource.Igdb;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class GameMetadataFixture : TestBase<IgdbMetadataProxy>
    {
        [Test]
        public void Should_resolve_IGameMetadataProxy_from_container()
        {
            var container = new Container(rules => rules.WithNzbDroneRules());
            container.AutoAddServices(new List<string> { "Sonarr.Core" });

            container.RegisterInstance(Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>().Object);
            container.RegisterInstance(Mocker.GetMock<NzbDrone.Core.Configuration.IConfigService>().Object);

            var resolvedProxies = container.Resolve<IEnumerable<IGameMetadataProxy>>();

            resolvedProxies.Should().Contain(p => p is IgdbMetadataProxy);
        }

        [Test]
        public async Task Should_return_empty_list_for_SearchPlatformsAsync()
        {
            var proxy = Subject;
            var platforms = await proxy.SearchPlatformsAsync("Nintendo");

            platforms.Should().NotBeNull();
            platforms.Should().BeEmpty();
        }

        [Test]
        public async Task Should_return_empty_list_for_SearchGamesAsync()
        {
            var proxy = Subject;
            var games = await proxy.SearchGamesAsync("Mario", 1);

            games.Should().NotBeNull();
            games.Should().BeEmpty();
        }

        [Test]
        public async Task Should_return_null_for_GetGameDetailsAsync()
        {
            var proxy = Subject;
            var game = await proxy.GetGameDetailsAsync(100);

            game.Should().BeNull();
        }

        [Test]
        public async Task Should_return_null_for_GetGameByHashAsync()
        {
            var proxy = Subject;
            var game = await proxy.GetGameByHashAsync("abcdef1234567890");

            game.Should().BeNull();
        }
    }
}
