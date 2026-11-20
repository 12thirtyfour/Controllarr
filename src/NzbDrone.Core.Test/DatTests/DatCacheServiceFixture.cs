using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DatTests
{
    [TestFixture]
    public class DatCacheServiceFixture : DbTest<DatCacheService, DatFile>
    {
        private DatFile _datFile;
        private List<DatEntry> _datEntries;

        [SetUp]
        public void Setup()
        {
            _datFile = new DatFile
            {
                Name = "Test Dat",
                Description = "A Test Dat",
                Version = "1.0",
                Author = "Tester",
                Category = "Console",
                CreatedAt = DateTime.UtcNow
            };

            _datEntries = new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Game 1",
                    RomName = "Game 1.rom",
                    SmdbPath = "1 USA/Game 1.rom",
                    Size = 1024,
                    Crc32 = "crc1",
                    Md5 = "md51",
                    Sha1 = "sha11",
                    Sha256 = "sha2561",
                    Region = "USA",
                    Status = "verified"
                },
                new DatEntry
                {
                    GameName = "Game 2",
                    RomName = "Game 2.rom",
                    Size = 2048,
                    Crc32 = "crc2",
                    Md5 = "md52",
                    Sha1 = "sha12",
                    Sha256 = "sha2562"
                }
            };
        }

        [Test]
        public void Should_insert_dat_file_and_entries()
        {
            var result = Subject.Insert(_datFile, _datEntries);

            result.Id.Should().BeGreaterThan(0);
            _datEntries[0].DatFileId.Should().Be(result.Id);
            _datEntries[1].DatFileId.Should().Be(result.Id);
        }

        [Test]
        public void Should_find_by_crc32()
        {
            Subject.Insert(_datFile, _datEntries);

            var entry = Subject.FindByCrc32("CRC1");

            entry.Should().NotBeNull();
            entry.GameName.Should().Be("Game 1");
        }

        [Test]
        public void Should_find_by_sha1()
        {
            Subject.Insert(_datFile, _datEntries);

            var entry = Subject.FindBySha1("SHA12");

            entry.Should().NotBeNull();
            entry.GameName.Should().Be("Game 2");
        }

        [Test]
        public void Should_find_by_md5()
        {
            Subject.Insert(_datFile, _datEntries);

            var entry = Subject.FindByMd5("MD51");

            entry.Should().NotBeNull();
            entry.GameName.Should().Be("Game 1");
        }

        [Test]
        public void Should_find_by_sha256_and_preserve_smdb_path()
        {
            Subject.Insert(_datFile, _datEntries);

            var entry = Subject.FindBySha256("SHA2561");

            entry.Should().NotBeNull();
            entry.GameName.Should().Be("Game 1");
            entry.SmdbPath.Should().Be("1 USA/Game 1.rom");
        }

        [Test]
        public void Should_return_null_when_hash_not_found()
        {
            Subject.Insert(_datFile, _datEntries);

            Subject.FindByCrc32("invalid").Should().BeNull();
            Subject.FindBySha1("invalid").Should().BeNull();
            Subject.FindByMd5("invalid").Should().BeNull();
            Subject.FindBySha256("invalid").Should().BeNull();
        }

        [Test]
        public void Should_replace_existing_dat_file_on_update_without_duplicating_rows()
        {
            _datFile.Description = "snes";
            Subject.Insert(_datFile, _datEntries);

            var updatedEntries = new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Chrono Trigger (USA)",
                    RomName = "Chrono Trigger (USA).sfc",
                    Crc32 = "b19ed489"
                }
            };

            Subject.Insert(_datFile, updatedEntries);

            Subject.GetAllDatFiles().Should().HaveCount(1);
            var platformEntries = Subject.GetEntriesForPlatform("snes", "Super Nintendo Entertainment System");
            platformEntries.Should().HaveCount(1);
            platformEntries[0].GameName.Should().Be("Chrono Trigger (USA)");
            Subject.FindByCrc32("crc1").Should().BeNull();
            Subject.FindByCrc32("b19ed489").Should().NotBeNull();
        }

        [Test]
        public void Should_not_return_snes_entries_when_querying_nes_platform()
        {
            var snesDatFile = new DatFile
            {
                Name = "Nintendo - Super Nintendo Entertainment System (SmokeMonster SMDB)",
                Description = "snes"
            };

            var snesEntries = new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Super Mario World (USA)",
                    RomName = "Super Mario World (USA).sfc",
                    Crc32 = "b19ed489"
                }
            };

            Subject.Insert(snesDatFile, snesEntries);

            var nesEntries = Subject.GetEntriesForPlatform("nes", "Nintendo Entertainment System", "Nintendo - Nintendo Entertainment System");
            nesEntries.Should().BeEmpty();

            var queriedSnesEntries = Subject.GetEntriesForPlatform("snes", "Super Nintendo Entertainment System", "Nintendo - Super Nintendo Entertainment System");
            queriedSnesEntries.Should().HaveCount(1);
            queriedSnesEntries[0].GameName.Should().Be("Super Mario World (USA)");
        }
    }
}
