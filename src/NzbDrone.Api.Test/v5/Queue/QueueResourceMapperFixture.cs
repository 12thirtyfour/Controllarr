using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Queue;

namespace NzbDrone.Api.Test.v5.Queue
{
    [TestFixture]
    public class QueueResourceMapperFixture : TestBase
    {
        [Test]
        public void should_empty_game_ids_when_full_season_collection_pack()
        {
            var queue = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Title = "Nintendo Game Boy Advance HTGDB Collection",
                Episodes = new List<Episode>
                {
                    new Episode { Id = 101, Title = "Game 1" },
                    new Episode { Id = 102, Title = "Game 2" },
                    new Episode { Id = 103, Title = "Game 3" }
                },
                RemoteEpisode = new RemoteEpisode
                {
                    ParsedEpisodeInfo = new ParsedEpisodeInfo
                    {
                        FullSeason = true
                    }
                }
            };

            var resource = queue.ToResource(includeSeries: false, includeEpisodes: false);

            resource.IsFullSeason.Should().BeTrue();
            resource.GameIds.Should().BeEmpty();
        }

        [Test]
        public void should_populate_game_ids_when_single_game()
        {
            var queue = new NzbDrone.Core.Queue.Queue
            {
                Id = 2,
                Title = "Pokemon Emerald",
                Episodes = new List<Episode>
                {
                    new Episode { Id = 201, Title = "Pokemon Emerald" }
                },
                RemoteEpisode = new RemoteEpisode
                {
                    ParsedEpisodeInfo = new ParsedEpisodeInfo
                    {
                        FullSeason = false
                    }
                }
            };

            var resource = queue.ToResource(includeSeries: false, includeEpisodes: false);

            resource.IsFullSeason.Should().BeFalse();
            resource.GameIds.Should().ContainSingle().Which.Should().Be(201);
        }
    }
}
