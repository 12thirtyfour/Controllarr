using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.PlatformTests
{
    [TestFixture]
    public class PlatformScannedHandlerFixture : CoreTest<PlatformScannedHandler>
    {
        [Test]
        public void should_queue_search_before_publishing_series_add_completed()
        {
            var series = Builder<Series>.CreateNew()
                                        .With(s => s.AddOptions = new AddPlatformOptions { SearchForMissingEpisodes = true })
                                        .Build();

            var calls = new List<string>();

            Mocker.GetMock<IManageCommandQueue>()
                  .Setup(s => s.Push(It.IsAny<MissingGameSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                  .Callback(() => calls.Add("search"));

            Mocker.GetMock<IEventAggregator>()
                  .Setup(s => s.PublishEvent(It.IsAny<PlatformAddCompletedEvent>()))
                  .Callback(() => calls.Add("addCompleted"));

            Subject.Handle(new PlatformScannedEvent(series, new List<string>()));

            calls.Should().Equal("search", "addCompleted");
        }
    }
}
