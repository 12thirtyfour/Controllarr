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
    public class DatParserFixture : CoreTest<DatParserService>
    {
        private const string ValidLogiqxXml = @"<?xml version=""1.0""?>
<!DOCTYPE datafile PUBLIC ""-//Logiqx//DTD ROM Management Datafile//EN"" ""http://www.logiqx.com/dtds/datafile.dtd"">
<datafile>
	<header>
		<name>Nintendo - Super Nintendo Entertainment System</name>
		<description>Nintendo - Super Nintendo Entertainment System</description>
		<version>20231015-182352</version>
		<author>No-Intro</author>
		<category>Console</category>
	</header>
	<game name=""Super Mario World (USA)"">
		<description>Super Mario World (USA)</description>
		<rom name=""Super Mario World (USA).sfc"" size=""524288"" crc=""B19ED489"" md5=""CDB36225DD1E7C4F22CDE6924DD79669"" sha1=""6B47BB75D16514B6A476AA0C73A683A2A4C18765"" status=""verified""/>
	</game>
    <machine name=""The Legend of Zelda (USA)"">
        <description>The Legend of Zelda (USA)</description>
        <rom name=""Zelda.nes"" size=""131088"" crc=""AB12CD34"" />
    </machine>
</datafile>";

        [Test]
        public void Should_parse_logiqx_xml_correctly()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidLogiqxXml)))
            {
                var (datFile, datEntries) = Subject.Parse(stream);

                datFile.Should().NotBeNull();
                datFile.Name.Should().Be("Nintendo - Super Nintendo Entertainment System");
                datFile.Description.Should().Be("Nintendo - Super Nintendo Entertainment System");
                datFile.Version.Should().Be("20231015-182352");
                datFile.Author.Should().Be("No-Intro");
                datFile.Category.Should().Be("Console");

                datEntries.Should().HaveCount(2);

                var mario = datEntries.First(e => e.GameName == "Super Mario World (USA)");
                mario.RomName.Should().Be("Super Mario World (USA).sfc");
                mario.Size.Should().Be(524288);
                mario.Crc32.Should().Be("b19ed489");
                mario.Md5.Should().Be("cdb36225dd1e7c4f22cde6924dd79669");
                mario.Sha1.Should().Be("6b47bb75d16514b6a476aa0c73a683a2a4c18765");
                mario.Status.Should().Be("verified");

                var zelda = datEntries.First(e => e.GameName == "The Legend of Zelda (USA)");
                zelda.RomName.Should().Be("Zelda.nes");
                zelda.Size.Should().Be(131088);
                zelda.Crc32.Should().Be("ab12cd34");
                zelda.Md5.Should().BeNull();
            }
        }

        [Test]
        public void Should_prefer_name_over_prose_description_in_clrmamepro_dat()
        {
            const string wiiUDat = @"
clrmamepro (
	name ""Nintendo - Wii U""
	description ""Nintendo - Wii U""
)
game (
	name ""Rodea the Sky Soldier (Japan)""
	description "",000 years ago, Emperor Geardo of the Naga Empire sent an army of machine soldiers to invade the sky kingdom of Garuda. Thanks to the...""
	rom ( name ""Rodea the Sky Soldier (Japan).wux"" size 1000 crc 11111111 )
)
game (
	name ""Monster High: 13 Wishes (USA)""
	description ""13 is the Magic Number for Monster High! What if a lamp was more than it seemed? What if Whisp had...won? Monster High: 13 Wishes is a...""
	rom ( name ""Monster High - 13 Wishes (USA).wux"" size 2000 crc 22222222 )
)
game (
	description ""♯FE is a crossover role-playing video game developed by Atlus and published by Nintendo for the Wii U home console. The game is based on...""
	rom ( name ""Gen'ei Ibunroku FE (Japan).wux"" size 3000 crc 33333333 )
)";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(wiiUDat)))
            {
                var (_, datEntries) = Subject.Parse(stream);

                datEntries.Should().HaveCount(3);
                datEntries[0].GameName.Should().Be("Rodea the Sky Soldier (Japan)");
                datEntries[1].GameName.Should().Be("Monster High: 13 Wishes (USA)");
                datEntries[2].GameName.Should().Be("Gen'ei Ibunroku FE (Japan)");
            }
        }

        [Test]
        public void Should_prefer_name_over_prose_description_in_xml_dat()
        {
            const string xmlWithProse = @"<?xml version=""1.0""?>
<datafile>
	<header>
		<name>Nintendo - Wii U</name>
	</header>
	<game name=""Rodea the Sky Soldier (USA)"">
		<description>,000 years ago, Emperor Geardo of the Naga Empire sent an army of machine soldiers to invade the sky kingdom of Garuda. Thanks to the...</description>
		<rom name=""Rodea the Sky Soldier (USA).wux"" size=""1024"" crc=""12345678"" />
	</game>
</datafile>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlWithProse)))
            {
                var (_, datEntries) = Subject.Parse(stream);

                datEntries.Should().ContainSingle();
                datEntries[0].GameName.Should().Be("Rodea the Sky Soldier (USA)");
            }
        }

        [Test]
        public void Should_use_description_for_short_arcade_romset_tokens()
        {
            const string clrArcade = @"
game (
	name ""sf2ce""
	description ""Street Fighter II': Champion Edition (World 920513)""
	rom ( name ""sf2ce.zip"" size 1024 crc 11223344 )
)";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(clrArcade)))
            {
                var (_, datEntries) = Subject.Parse(stream);

                datEntries.Should().ContainSingle();
                datEntries[0].GameName.Should().Be("Street Fighter II': Champion Edition (World 920513)");
            }

            const string xmlArcade = @"<?xml version=""1.0""?>
<datafile>
	<header><name>MAME</name></header>
	<machine name=""mslug"">
		<description>Metal Slug - Super Vehicle-001</description>
		<rom name=""mslug.zip"" size=""2048"" crc=""55667788"" />
	</machine>
</datafile>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlArcade)))
            {
                var (_, datEntries) = Subject.Parse(stream);

                datEntries.Should().ContainSingle();
                datEntries[0].GameName.Should().Be("Metal Slug - Super Vehicle-001");
            }
        }

        [Test]
        public void Should_deduplicate_and_merge_entries_sharing_hashes_within_dat_file()
        {
            const string duplicateXmlDat = @"<?xml version=""1.0""?>
<datafile>
	<header>
		<name>Nintendo - Super Nintendo Entertainment System</name>
	</header>
	<game name=""Super Mario World (USA)"">
		<rom name=""Super Mario World (USA).sfc"" size=""524288"" crc=""B19ED489"" sha1=""6B47BB75D16514B6A476AA0C73A683A2A4C18765"" />
	</game>
	<game name=""Super Mario World (USA) (Alt)"">
		<rom name=""Super Mario World (USA) (Alt).sfc"" size=""524288"" md5=""CDB36225DD1E7C4F22CDE6924DD79669"" sha1=""6b47bb75d16514b6a476aa0c73a683a2a4c18765"" sha256=""0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"" />
	</game>
</datafile>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(duplicateXmlDat)))
            {
                var (_, datEntries) = Subject.Parse(stream);

                datEntries.Should().ContainSingle();
                datEntries[0].Crc32.Should().Be("b19ed489");
                datEntries[0].Md5.Should().Be("cdb36225dd1e7c4f22cde6924dd79669");
                datEntries[0].Sha1.Should().Be("6b47bb75d16514b6a476aa0c73a683a2a4c18765");
                datEntries[0].Sha256.Should().Be("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            }
        }

        [Test]
        public void Should_prefix_isbios_entries_with_bios_tag_in_xml_and_clrmamepro()
        {
            const string xmlWithBios = @"<?xml version=""1.0""?>
<datafile>
	<header><name>Sega - Saturn</name></header>
	<game name=""sega_101"" isbios=""yes"">
		<description>Saturn System Software</description>
		<rom name=""sega_101.bin"" size=""524288"" crc=""AABBCCDD"" />
	</game>
</datafile>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlWithBios)))
            {
                var (_, datEntries) = Subject.Parse(stream);
                datEntries.Should().ContainSingle();
                datEntries[0].GameName.Should().StartWith("[BIOS] ");
            }

            const string clrWithBios = @"
game (
	name ""System Unit v1.0 (World)""
	isbios yes
	rom ( name ""sysunit.bin"" size 65536 crc 11223344 )
)";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(clrWithBios)))
            {
                var (_, datEntries) = Subject.Parse(stream);
                datEntries.Should().ContainSingle();
                datEntries[0].GameName.Should().Be("[BIOS] System Unit v1.0 (World)");
            }
        }
    }
}
