using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.PlatformTests.GameServiceTests
{
    [TestFixture]
    public class GetGamesByIdsFixture : CoreTest<EpisodeService>
    {
        [Test]
        public void should_deduplicate_ids_when_fetching_games()
        {
            var game1 = Builder<Episode>.CreateNew().With(e => e.Id = 10).Build();
            var game2 = Builder<Episode>.CreateNew().With(e => e.Id = 20).Build();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.Get(It.Is<IEnumerable<int>>(ids => ids != null)))
                  .Returns(new List<Episode> { game1, game2 });

            var result = Subject.GetEpisodes(new[] { 10, 20, 10, 0, -1 });

            result.Should().HaveCount(2);
            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(r => r.Get(It.Is<IEnumerable<int>>(ids => System.Linq.Enumerable.SequenceEqual(ids, new[] { 10, 20 }))), Times.Once());
        }

        [Test]
        public void should_return_existing_games_when_some_ids_no_longer_exist()
        {
            var game1 = Builder<Episode>.CreateNew().With(e => e.Id = 10).Build();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.Get(It.IsAny<IEnumerable<int>>()))
                  .Throws(new ApplicationException("Expected query to return 2 rows but returned 1"));

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.Get(10))
                  .Returns(game1);

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.Get(999))
                  .Throws(new ModelNotFoundException(typeof(Episode), 999));

            var result = Subject.GetEpisodes(new[] { 10, 999 });

            result.Should().ContainSingle(e => e.Id == 10);
        }
    }
}
