using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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
    public class RomEntryControllerFixture : TestBase<RomEntryController>
    {
        [Test]
        public void should_have_v3_api_controller_attribute()
        {
            var attributes = typeof(RomEntryController).GetCustomAttributes(typeof(V3ApiControllerAttribute), true);
            attributes.Should().HaveCount(1);

            var attr = (V3ApiControllerAttribute)attributes.First();
            attr.Template.Should().Be("api/v3/romentry");
        }

        [Test]
        public void should_get_rom_entries_by_platform_id()
        {
            var roms = new List<RomEntry>
            {
                new RomEntry
                {
                    Id = 1,
                    PlatformId = 10,
                    Title = "Super Mario World",
                    CleanTitle = "supermariobueno",
                    Region = "USA",
                    Language = "En",
                    Revision = "Rev 1",
                    Hash = "ABC123HASH"
                }
            };

            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.GetByPlatform(10))
                .Returns(roms);

            var result = Subject.GetRomEntries(10);

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
            result[0].PlatformId.Should().Be(10);
            result[0].Title.Should().Be("Super Mario World");
            result[0].Hash.Should().Be("ABC123HASH");
        }

        [Test]
        public void should_get_all_rom_entries_when_no_platform_id()
        {
            var roms = new List<RomEntry>
            {
                new RomEntry { Id = 1, PlatformId = 10, Title = "Super Mario World" },
                new RomEntry { Id = 2, PlatformId = 20, Title = "Sonic the Hedgehog" }
            };

            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.All())
                .Returns(roms);

            var result = Subject.GetRomEntries(null);

            result.Should().HaveCount(2);
        }

        [Test]
        public void should_get_rom_entry_by_hash()
        {
            var rom = new RomEntry
            {
                Id = 1,
                PlatformId = 10,
                Title = "Super Mario World",
                Hash = "ABC123HASH"
            };

            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.GetByHash("ABC123HASH"))
                .Returns(rom);

            var actionResult = Subject.GetByHash("ABC123HASH");

            actionResult.Result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)actionResult.Result;
            var resource = (RomEntryResource)okResult.Value;
            resource.Title.Should().Be("Super Mario World");
            resource.Hash.Should().Be("ABC123HASH");
        }

        [Test]
        public void should_return_not_found_when_hash_not_found()
        {
            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.GetByHash("NONEXISTENT"))
                .Returns((RomEntry)null);

            var actionResult = Subject.GetByHash("NONEXISTENT");

            actionResult.Result.Should().BeOfType<NotFoundResult>();
        }

        [Test]
        public void should_create_rom_entry()
        {
            var resource = new RomEntryResource
            {
                PlatformId = 10,
                Title = "Chrono Trigger",
                Hash = "DEF456HASH"
            };

            var insertedRom = new RomEntry
            {
                Id = 5,
                PlatformId = 10,
                Title = "Chrono Trigger",
                Hash = "DEF456HASH"
            };

            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.Insert(It.Is<RomEntry>(m => m.Title == "Chrono Trigger")))
                .Returns(insertedRom);

            Mocker.GetMock<IRomEntryRepository>()
                .Setup(r => r.Get(5))
                .Returns(insertedRom);

            var actionResult = Subject.Create(resource);

            actionResult.Should().NotBeNull();
            Mocker.GetMock<IRomEntryRepository>().Verify(r => r.Insert(It.Is<RomEntry>(m => m.Title == "Chrono Trigger")), Times.Once());
        }

        [Test]
        public void should_delete_rom_entry()
        {
            Subject.DeleteRomEntry(1);

            Mocker.GetMock<IRomEntryRepository>().Verify(r => r.Delete(1), Times.Once());
        }
    }
}
