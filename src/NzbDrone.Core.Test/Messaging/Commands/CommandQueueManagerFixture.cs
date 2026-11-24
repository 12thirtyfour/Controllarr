using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Dat.Commands;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Test.Messaging.Commands
{
    [TestFixture]
    public class CommandQueueManagerFixture : CoreTest<CommandQueueManager>
    {
        [SetUp]
        public void Setup()
        {
            var id = 0;
            var commands = new List<CommandModel>();

            Mocker.GetMock<ICommandRepository>()
                  .Setup(s => s.Insert(It.IsAny<CommandModel>()))
                  .Returns<CommandModel>(c =>
                  {
                      c.Id = id + 1;
                      commands.Add(c);
                      id++;

                      return c;
                  });

            Mocker.GetMock<ICommandRepository>()
                  .Setup(s => s.Get(It.IsAny<int>()))
                  .Returns<int>(c =>
                  {
                      return commands.SingleOrDefault(e => e.Id == c);
                  });
        }

        [Test]
        public void should_not_remove_commands_for_five_minutes_after_they_end()
        {
            var command = Subject.Push<RefreshMonitoredDownloadsCommand>(new RefreshMonitoredDownloadsCommand());

            // Start the command to mimic CommandQueue's behaviour
            command.StartedAt = DateTime.Now;
            command.Status = CommandStatus.Started;

            Subject.Start(command);
            Subject.Complete(command, "All done");
            Subject.CleanCommands();

            Subject.Get(command.Id).Should().NotBeNull();

            Mocker.GetMock<ICommandRepository>()
                  .Verify(v => v.Get(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_block_library_refresh_and_scan_while_dat_update_is_queued_or_started()
        {
            var queue = new CommandQueue();
            var now = DateTime.UtcNow;

            var refreshCommand = new CommandModel
            {
                Id = 1,
                Name = "RefreshPlatform",
                Body = new RefreshPlatformCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now
            };

            var rescanCommand = new CommandModel
            {
                Id = 2,
                Name = "RescanPlatform",
                Body = new RescanPlatformCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now.AddSeconds(1)
            };

            var datUpdateCommand = new CommandModel
            {
                Id = 3,
                Name = "UpdateDatCatalogs",
                Body = new UpdateDatCatalogsCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now.AddSeconds(2)
            };

            var downloadCheckCommand = new CommandModel
            {
                Id = 4,
                Name = "RefreshMonitoredDownloads",
                Body = new RefreshMonitoredDownloadsCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now.AddSeconds(3)
            };

            queue.Add(refreshCommand);
            queue.Add(rescanCommand);
            queue.Add(datUpdateCommand);
            queue.Add(downloadCheckCommand);

            queue.TryGet(out var first).Should().BeTrue();
            first.Should().BeSameAs(datUpdateCommand);

            queue.TryGet(out var second).Should().BeTrue();
            second.Should().BeSameAs(downloadCheckCommand);

            queue.TryGet(out var third).Should().BeFalse();

            datUpdateCommand.Status = CommandStatus.Completed;
            queue.TryGet(out var fourth).Should().BeTrue();
            fourth.Should().BeSameAs(refreshCommand);

            queue.TryGet(out var fifth).Should().BeTrue();
            fifth.Should().BeSameAs(rescanCommand);
        }

        [Test]
        public void should_block_library_refresh_and_scan_while_dat_update_scope_is_active()
        {
            var queue = new CommandQueue();
            var now = DateTime.UtcNow;

            var refreshCommand = new CommandModel
            {
                Id = 1,
                Name = "RefreshPlatform",
                Body = new RefreshPlatformCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now
            };

            var rescanCommand = new CommandModel
            {
                Id = 2,
                Name = "RescanPlatform",
                Body = new RescanPlatformCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now.AddSeconds(1)
            };

            queue.Add(refreshCommand);
            queue.Add(rescanCommand);

            using (DatCatalogUpdateService.BeginDatUpdateScope())
            {
                queue.TryGet(out _).Should().BeFalse();
            }

            queue.TryGet(out var next).Should().BeTrue();
            next.Should().BeSameAs(refreshCommand);
        }

        [Test]
        public void should_wait_for_started_library_refresh_before_starting_dat_update()
        {
            var queue = new CommandQueue();
            var now = DateTime.UtcNow;

            var refreshCommand = new CommandModel
            {
                Id = 1,
                Name = "RefreshPlatform",
                Body = new RefreshPlatformCommand(),
                Status = CommandStatus.Started,
                Priority = CommandPriority.Normal,
                QueuedAt = now
            };

            var datUpdateCommand = new CommandModel
            {
                Id = 2,
                Name = "UpdateDatCatalogs",
                Body = new UpdateDatCatalogsCommand(),
                Status = CommandStatus.Queued,
                Priority = CommandPriority.Normal,
                QueuedAt = now.AddSeconds(1)
            };

            queue.Add(refreshCommand);
            queue.Add(datUpdateCommand);

            queue.TryGet(out _).Should().BeFalse();

            refreshCommand.Status = CommandStatus.Completed;

            queue.TryGet(out var next).Should().BeTrue();
            next.Should().BeSameAs(datUpdateCommand);
        }
    }
}
