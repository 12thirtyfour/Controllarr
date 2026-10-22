using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class GamePlatformRepositoryFixture : DbTest<GamePlatformRepository, GamePlatform>
    {
        [Test]
        public void should_insert_and_retrieve_game_platform()
        {
            var platform = new GamePlatform
            {
                Name = "Super Nintendo Entertainment System",
                Slug = "snes",
                Code = "SNES",
                IgdbId = 19,
                Overview = "16-bit console",
                Path = "/games/snes",
                Monitored = true,
                RootFolderPath = "/games",
                CollectionStrategy = CollectionStrategy.OneGameOneRom,
                RevisionPreference = RevisionPreference.LatestRevision,
                ReleaseYear = 1990
            };

            var inserted = Subject.Insert(platform);
            inserted.Id.Should().BeGreaterThan(0);

            var retrieved = Subject.Get(inserted.Id);
            retrieved.Should().NotBeNull();
            retrieved.Name.Should().Be("Super Nintendo Entertainment System");
            retrieved.Slug.Should().Be("snes");
            retrieved.Code.Should().Be("SNES");
            retrieved.ReleaseYear.Should().Be(1990);
        }

        [Test]
        public void should_get_by_slug()
        {
            var platform = new GamePlatform
            {
                Name = "Sega Genesis",
                Slug = "genesis",
                Code = "GEN"
            };

            Subject.Insert(platform);

            var result = Subject.GetBySlug("genesis");
            result.Should().NotBeNull();
            result.Name.Should().Be("Sega Genesis");
        }

        [Test]
        public void should_get_by_code()
        {
            var platform = new GamePlatform
            {
                Name = "Game Boy",
                Slug = "gameboy",
                Code = "GB"
            };

            Subject.Insert(platform);

            var result = Subject.GetByCode("GB");
            result.Should().NotBeNull();
            result.Name.Should().Be("Game Boy");
        }
    }

    [TestFixture]
    public class RomEntryRepositoryFixture : DbTest<RomEntryRepository, RomEntry>
    {
        private GamePlatform _platform1;
        private GamePlatform _platform2;

        [SetUp]
        public void Setup()
        {
            var platformRepo = Mocker.Resolve<GamePlatformRepository>();

            _platform1 = platformRepo.Insert(new GamePlatform
            {
                Name = "Platform 1",
                Slug = "platform-1",
                Code = "P1"
            });

            _platform2 = platformRepo.Insert(new GamePlatform
            {
                Name = "Platform 2",
                Slug = "platform-2",
                Code = "P2"
            });
        }

        [Test]
        public void should_insert_and_retrieve_rom_entry()
        {
            var rom = new RomEntry
            {
                PlatformId = _platform1.Id,
                Title = "Super Mario World",
                CleanTitle = "supermarioworld",
                Region = "USA",
                Language = "En",
                Revision = "Rev 1",
                Hash = "1234567890abcdef"
            };

            var inserted = Subject.Insert(rom);
            inserted.Id.Should().BeGreaterThan(0);

            var retrieved = Subject.Get(inserted.Id);
            retrieved.Should().NotBeNull();
            retrieved.Title.Should().Be("Super Mario World");
            retrieved.Hash.Should().Be("1234567890abcdef");
        }

        [Test]
        public void should_get_by_platform()
        {
            var rom1 = new RomEntry
            {
                PlatformId = _platform1.Id,
                Title = "Game A",
                CleanTitle = "gamea"
            };

            var rom2 = new RomEntry
            {
                PlatformId = _platform1.Id,
                Title = "Game B",
                CleanTitle = "gameb"
            };

            var rom3 = new RomEntry
            {
                PlatformId = _platform2.Id,
                Title = "Game C",
                CleanTitle = "gamec"
            };

            Subject.Insert(rom1);
            Subject.Insert(rom2);
            Subject.Insert(rom3);

            var results = Subject.GetByPlatform(_platform1.Id);
            results.Should().HaveCount(2);
            results.Select(r => r.Title).Should().Contain(new[] { "Game A", "Game B" });
        }

        [Test]
        public void should_get_by_hash()
        {
            var rom = new RomEntry
            {
                PlatformId = _platform1.Id,
                Title = "Chrono Trigger",
                CleanTitle = "chronotrigger",
                Hash = "abcdef1234567890"
            };

            Subject.Insert(rom);

            var result = Subject.GetByHash("abcdef1234567890");
            result.Should().NotBeNull();
            result.Title.Should().Be("Chrono Trigger");
        }
    }
}
