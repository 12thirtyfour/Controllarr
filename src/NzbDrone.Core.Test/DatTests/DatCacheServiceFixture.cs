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

        [Test]
        public void Should_prefer_strongest_hash_tier_and_matching_platform_without_limit_truncation()
        {
            var nesDat = new DatFile
            {
                Name = "Nintendo - Nintendo Entertainment System",
                Description = "nes"
            };
            var snesDat = new DatFile
            {
                Name = "Nintendo - Super Nintendo Entertainment System",
                Description = "snes"
            };

            var nesEntries = new List<DatEntry>();
            for (var i = 0; i < 30; i++)
            {
                nesEntries.Add(new DatEntry
                {
                    GameName = $"Noise Game {i}",
                    RomName = $"Noise Game {i}.nes",
                    Crc32 = "deadbeef",
                    Md5 = $"noise_md5_{i}",
                    Sha1 = $"noise_sha1_{i}",
                    Sha256 = $"noise_sha256_{i}"
                });
            }

            nesEntries.Add(new DatEntry
            {
                GameName = "Exact Strong Hash NES",
                RomName = "Exact Strong Hash NES.nes",
                Crc32 = "deadbeef",
                Md5 = "target_md5",
                Sha1 = "target_sha1",
                Sha256 = "target_sha256"
            });

            var snesEntries = new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Exact Strong Hash SNES",
                    RomName = "Exact Strong Hash SNES.sfc",
                    Crc32 = "deadbeef",
                    Md5 = "target_md5",
                    Sha1 = "target_sha1",
                    Sha256 = "target_sha256"
                }
            };

            Subject.Insert(nesDat, nesEntries);
            Subject.Insert(snesDat, snesEntries);

            var match = Subject.FindBestMatch(
                crc32: "deadbeef",
                md5: "target_md5",
                sha1: "target_sha1",
                sha256: "target_sha256",
                platformSlug: "snes",
                platformName: "Super Nintendo Entertainment System");

            match.Should().NotBeNull();
            match.GameName.Should().Be("Exact Strong Hash SNES");
        }

        [Test]
        public void Should_deduplicate_and_merge_hashes_across_sources_for_same_platform()
        {
            var noIntroDat = new DatFile
            {
                Name = "Super Nintendo Entertainment System (No-Intro)",
                Description = "snes",
                Category = "No-Intro"
            };

            var smdbDat = new DatFile
            {
                Name = "Super Nintendo Entertainment System (SmokeMonster SMDB)",
                Description = "snes",
                Category = "SmokeMonster SMDB"
            };

            Subject.Insert(noIntroDat, new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Super Mario World (USA)",
                    RomName = "Super Mario World (USA).sfc",
                    Size = 524288,
                    Crc32 = "B19ED489",
                    Md5 = "CDB36225DD1E7C4F22CDE6924DD79669",
                    Sha1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765"
                }
            });

            Subject.Insert(smdbDat, new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Super Mario World (USA)",
                    RomName = "Super Mario World (USA).sfc",
                    SmdbPath = "1 US/Super Mario World (USA).sfc",
                    Sha256 = "AABBCCDD11223344AABBCCDD11223344AABBCCDD11223344AABBCCDD11223344",
                    Sha1 = "6b47bb75d16514b6a476aa0c73a683a2a4c18765"
                },
                new DatEntry
                {
                    GameName = "SMDB Exclusive Hack (USA)",
                    RomName = "SMDB Exclusive Hack (USA).sfc",
                    SmdbPath = "4 Hacks/SMDB Exclusive Hack (USA).sfc",
                    Sha256 = "1111222233334444111122223333444411112222333344441111222233334444",
                    Sha1 = "0000111122223333444455556666777788889999"
                }
            });

            var snesEntries = Subject.GetEntriesForPlatform("snes", "Super Nintendo Entertainment System");
            snesEntries.Should().HaveCount(2);

            var mario = snesEntries.Find(e => e.GameName == "Super Mario World (USA)");
            mario.Should().NotBeNull();
            mario.Crc32.Should().Be("b19ed489");
            mario.Md5.Should().Be("cdb36225dd1e7c4f22cde6924dd79669");
            mario.Sha1.Should().Be("6b47bb75d16514b6a476aa0c73a683a2a4c18765");
            mario.Sha256.Should().Be("aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344");
            mario.SmdbPath.Should().Be("1 US/Super Mario World (USA).sfc");
        }

        [Test]
        public void Should_deduplicate_and_merge_within_single_insert_batch()
        {
            var dat = new DatFile
            {
                Name = "Super Nintendo Entertainment System (Combined)",
                Description = "snes"
            };

            Subject.Insert(dat, new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "Chrono Trigger (USA)",
                    RomName = "Chrono Trigger (USA).sfc",
                    Crc32 = "2D206BF7",
                    Sha1 = "AAAA111122223333444455556666777788889999"
                },
                new DatEntry
                {
                    GameName = "Chrono Trigger (USA) Alt",
                    RomName = "Chrono Trigger (USA) Alt.sfc",
                    Md5 = "BBBB1111222233334444555566667777",
                    Sha1 = "aaaa111122223333444455556666777788889999",
                    Sha256 = "CCCC111122223333444455556666777788889999000011112222333344445555"
                }
            });

            var snesEntries = Subject.GetEntriesForPlatform("snes", "Super Nintendo Entertainment System");
            snesEntries.Should().ContainSingle();
            snesEntries[0].Crc32.Should().Be("2d206bf7");
            snesEntries[0].Md5.Should().Be("bbbb1111222233334444555566667777");
            snesEntries[0].Sha256.Should().Be("cccc111122223333444455556666777788889999000011112222333344445555");
        }
    }
}
