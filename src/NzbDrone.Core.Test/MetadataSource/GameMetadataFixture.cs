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
    public class GameMetadataFixture : TestBase
    {
        [Test]
        public void Should_resolve_IGameMetadataProxy_from_container()
        {
            var container = new Container(rules => rules.WithNzbDroneRules());
            container.AutoAddServices(new List<string> { "Sonarr.Core" });

            var resolvedProxy = container.Resolve<IGameMetadataProxy>();

            resolvedProxy.Should().NotBeNull();
            resolvedProxy.Should().BeOfType<IgdbMetadataProxy>();
        }

        [Test]
        public async Task Should_return_empty_list_for_SearchPlatformsAsync()
        {
            var proxy = new IgdbMetadataProxy();
            var platforms = await proxy.SearchPlatformsAsync("Nintendo");

            platforms.Should().NotBeNull();
            platforms.Should().BeEmpty();
        }

        [Test]
        public async Task Should_return_empty_list_for_SearchGamesAsync()
        {
            var proxy = new IgdbMetadataProxy();
            var games = await proxy.SearchGamesAsync("Mario", 1);

            games.Should().NotBeNull();
            games.Should().BeEmpty();
        }

        [Test]
        public async Task Should_return_null_for_GetGameDetailsAsync()
        {
            var proxy = new IgdbMetadataProxy();
            var game = await proxy.GetGameDetailsAsync(100);

            game.Should().BeNull();
        }

        [Test]
        public async Task Should_return_null_for_GetGameByHashAsync()
        {
            var proxy = new IgdbMetadataProxy();
            var game = await proxy.GetGameByHashAsync("abcdef1234567890");

            game.Should().BeNull();
        }
    }
}
