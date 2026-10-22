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
    }
}
