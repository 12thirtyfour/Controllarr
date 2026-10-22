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
                    Size = 1024,
                    Crc32 = "crc1",
                    Md5 = "md51",
                    Sha1 = "sha11",
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
                    Sha1 = "sha12"
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
        public void Should_return_null_when_hash_not_found()
        {
            Subject.Insert(_datFile, _datEntries);

            Subject.FindByCrc32("invalid").Should().BeNull();
            Subject.FindBySha1("invalid").Should().BeNull();
            Subject.FindByMd5("invalid").Should().BeNull();
        }
    }
}
