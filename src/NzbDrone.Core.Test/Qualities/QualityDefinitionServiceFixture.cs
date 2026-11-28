using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Qualities.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityDefinitionServiceFixture : CoreTest<QualityDefinitionService>
    {
        [Test]
        public void init_should_add_all_definitions()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<QualityDefinition>>(d => d.Count == Quality.All.Count)), Times.Once());
        }

        [Test]
        public void init_should_insert_any_missing_definitions()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                              new QualityDefinition(Quality.VerifiedGoodDump) { Weight = 103, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<QualityDefinition>>(d => d.Count == Quality.All.Count - 1)), Times.Once());
        }

        [Test]
        public void init_should_update_existing_definitions()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                              new QualityDefinition(Quality.VerifiedGoodDump) { Weight = 103, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.UpdateMany(It.Is<List<QualityDefinition>>(d => d.Count == 1)), Times.Once());
        }

        [Test]
        public void init_should_remove_old_definitions()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                              new QualityDefinition(new Quality { Id = 1000, Name = "Test" }) { Weight = 1, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.DeleteMany(It.Is<List<QualityDefinition>>(d => d.Count == 1)), Times.Once());
        }

        [Test]
        public void init_should_deduplicate_legacy_definitions_mapped_to_unknown()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                          new QualityDefinition(Quality.Unknown) { Id = 1, Title = "Legacy-Quality-1", Weight = 1 },
                          new QualityDefinition(Quality.Unknown) { Id = 2, Title = "Legacy-Quality-2", Weight = 1 },
                          new QualityDefinition(Quality.Unknown) { Id = 3, Title = "Unknown", Weight = 1 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<QualityDefinition>>(d => d.Count == Quality.All.Count - 1)), Times.Once());
            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.UpdateMany(It.Is<List<QualityDefinition>>(d => d.Count == 1 && d[0].Id == 3 && d[0].Title == "Unknown")), Times.Once());
            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.DeleteMany(It.Is<List<QualityDefinition>>(d => d.Count == 2 && d.Any(x => x.Id == 1) && d.Any(x => x.Id == 2))), Times.Once());
        }

        [Test]
        public void all_and_get_should_deduplicate_duplicate_unknown_definitions()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                          new QualityDefinition(Quality.Unknown) { Id = 1, Title = "Legacy-Quality-1" },
                          new QualityDefinition(Quality.Unknown) { Id = 3, Title = "Unknown" },
                          new QualityDefinition(Quality.VerifiedGoodDump) { Id = 10, Title = "VerifiedGoodDump" }
                      });

            var all = Subject.All();

            all.Should().HaveCount(2);
            Subject.Get(Quality.Unknown).Id.Should().Be(3);
            Subject.Get(Quality.Unknown).Title.Should().Be("Unknown");
        }

        [Test]
        public void reset_should_deduplicate_and_insert_missing_definitions()
        {
            Mocker.GetMock<IQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityDefinition>
                      {
                          new QualityDefinition(Quality.Unknown) { Id = 1, Title = "Legacy-Quality-1" },
                          new QualityDefinition(Quality.Unknown) { Id = 3, Title = "Unknown" }
                      });

            Subject.Execute(new ResetQualityDefinitionsCommand(true));

            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<QualityDefinition>>(d => d.Count == Quality.All.Count - 1)), Times.Once());
            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.UpdateMany(It.Is<List<QualityDefinition>>(d => d.Count == 1 && d[0].Id == 3)), Times.Once());
            Mocker.GetMock<IQualityDefinitionRepository>()
                .Verify(v => v.DeleteMany(It.Is<List<QualityDefinition>>(d => d.Count == 1 && d[0].Id == 1)), Times.Once());
        }
    }
}
