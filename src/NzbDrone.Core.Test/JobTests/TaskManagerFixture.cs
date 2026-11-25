using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Dat.Commands;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Test.JobTests
{
    [TestFixture]
    public class TaskManagerFixture : CoreTest<TaskManager>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());

            Mocker.GetMock<IConfigService>()
                .SetupGet(c => c.BackupInterval)
                .Returns(7);

            Mocker.GetMock<IConfigService>()
                .SetupGet(c => c.RssSyncInterval)
                .Returns(15);

            Mocker.GetMock<IScheduledTaskRepository>()
                .Setup(r => r.All())
                .Returns(new List<ScheduledTask>());
        }

        [Test]
        public void should_not_schedule_automatic_library_refresh_on_startup()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IScheduledTaskRepository>()
                .Verify(r => r.Upsert(It.Is<ScheduledTask>(t => t.TypeName == typeof(RefreshPlatformCommand).FullName)), Times.Never());

            var scheduledTasks = Subject.GetAll();
            scheduledTasks.Should().NotContain(t => t.TypeName == typeof(RefreshPlatformCommand).FullName);
            scheduledTasks.Should().Contain(t => t.TypeName == typeof(UpdateDatCatalogsCommand).FullName);
        }

        [Test]
        public void should_remove_legacy_scheduled_refresh_platform_task_on_startup()
        {
            Mocker.GetMock<IScheduledTaskRepository>()
                .Setup(r => r.All())
                .Returns(new List<ScheduledTask>
                {
                    new ScheduledTask
                    {
                        Id = 42,
                        TypeName = typeof(RefreshPlatformCommand).FullName,
                        Interval = 12 * 60
                    }
                });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IScheduledTaskRepository>()
                .Verify(r => r.Delete(42), Times.Once());
        }
    }
}
