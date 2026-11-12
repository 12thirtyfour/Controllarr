using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DatTests
{
    [TestFixture]
    public class SmdbParserFixture : CoreTest<SmdbParserService>
    {
        private const string ValidSmdbContent =
            "# Hardware Target Game Database SMDB\n" +
            "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2\t1 USA/A-F/Chrono Trigger (USA).sfc\t6b47bb75d16514b6a476aa0c73a683a2a4c18765\tcdb36225dd1e7c4f22cde6924dd79669\tb19ed489\t4194304\n" +
            "f6e5d4c3b2a1f6e5d4c3b2a1f6e5d4c3b2a1f6e5d4c3b2a1f6e5d4c3b2a1f6e5\t2 Japan/S-Z/Super Mario World (Japan).sfc\t1122334455667788990011223344556677889900\t99887766554433221100998877665544\tab12cd34\n";

        [Test]
        public void Should_parse_smdb_tab_delimited_records_correctly()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidSmdbContent)))
            {
                var (datFile, datEntries) = Subject.Parse(stream, "SNES SMDB");

                datFile.Should().NotBeNull();
                datFile.Name.Should().Be("SNES SMDB");
                datFile.Author.Should().Be("HTGDB");
                datFile.Category.Should().Be("SMDB");

                datEntries.Should().HaveCount(2);

                var chrono = datEntries.First(e => e.RomName == "Chrono Trigger (USA).sfc");
                chrono.GameName.Should().Be("Chrono Trigger (USA)");
                chrono.SmdbPath.Should().Be("1 USA/A-F/Chrono Trigger (USA).sfc");
                chrono.Sha256.Should().Be("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
                chrono.Sha1.Should().Be("6b47bb75d16514b6a476aa0c73a683a2a4c18765");
                chrono.Md5.Should().Be("cdb36225dd1e7c4f22cde6924dd79669");
                chrono.Crc32.Should().Be("b19ed489");
                chrono.Size.Should().Be(4194304);
                chrono.Region.Should().Be("USA");
                chrono.Status.Should().Be("verified");

                var mario = datEntries.First(e => e.RomName == "Super Mario World (Japan).sfc");
                mario.SmdbPath.Should().Be("2 Japan/S-Z/Super Mario World (Japan).sfc");
                mario.Region.Should().Be("Japan");
                mario.Size.Should().Be(0);
            }
        }

        [Test]
        public void Should_exclude_audio_files_and_secondary_tracks_and_clean_track_one()
        {
            var content =
                "1111111111111111111111111111111111111111111111111111111111111111\tSega CD/1 Releases/Sonic CD (USA)/Sonic CD (USA) (Track 1).bin\t1111111111111111111111111111111111111111\taaaaaaaa\t11111111\t100000\n" +
                "2222222222222222222222222222222222222222222222222222222222222222\tSega CD/1 Releases/Sonic CD (USA)/Sonic CD (USA) (Track 2).bin\t2222222222222222222222222222222222222222\tbbbbbbbb\t22222222\t200000\n" +
                "3333333333333333333333333333333333333333333333333333333333333333\tSega CD/1 Releases/Sonic CD (USA)/Sonic CD (USA).cue\t3333333333333333333333333333333333333333\tcccccccc\t33333333\t300\n" +
                "4444444444444444444444444444444444444444444444444444444444444444\tMegaSD/3 MegaSD MD+/Golden Axe Symphony MD+/03TurtleVillage2.wav\t4444444444444444444444444444444444444444\tdddddddd\t44444444\t400000\n";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                var (datFile, datEntries) = Subject.Parse(stream, "Sega CD SMDB");

                datEntries.Should().HaveCount(1);
                var entry = datEntries.Single();
                entry.GameName.Should().Be("Sonic CD (USA)");
                entry.RomName.Should().Be("Sonic CD (USA).bin");
                entry.Crc32.Should().Be("11111111");
            }
        }
    }
}
