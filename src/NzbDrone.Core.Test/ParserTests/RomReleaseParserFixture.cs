using System.Collections.Generic;
using DryIoc;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Core.Parser.Rom;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class RomReleaseParserFixture
    {
        private RomReleaseParser _subject;

        [SetUp]
        public void SetUp()
        {
            _subject = new RomReleaseParser();
        }

        [Test]
        public void Should_parse_super_mario_world_verified_dump()
        {
            var result = _subject.Parse("Super Mario World (USA) [!]");

            result.Should().NotBeNull();
            result.GameTitle.Should().Be("Super Mario World");
            result.Regions.Should().ContainSingle().Which.Should().Be("USA");
            result.IsVerifiedDump.Should().BeTrue();
            result.FormatExtension.Should().BeNull();
        }

        [Test]
        public void Should_parse_pokemon_ruby_version()
        {
            var result = _subject.Parse("Pokemon - Ruby Version (USA, Europe) (Rev 1).gba");

            result.Should().NotBeNull();
            result.GameTitle.Should().Be("Pokemon - Ruby Version");
            result.PlatformTag.Should().Be("GBA");
            result.Regions.Should().BeEquivalentTo("USA", "Europe");
            result.Revision.Should().Be("Rev 1");
            result.FormatExtension.Should().Be(".gba");
            result.IsVerifiedDump.Should().BeFalse();
        }

        [Test]
        public void Should_parse_final_fantasy_vii()
        {
            var result = _subject.Parse("Final Fantasy VII (USA) (Disc 1) (v1.1).chd");

            result.Should().NotBeNull();
            result.GameTitle.Should().Be("Final Fantasy VII");
            result.Regions.Should().ContainSingle().Which.Should().Be("USA");
            result.Revision.Should().Be("v1.1");
            result.FormatExtension.Should().Be(".chd");
            result.IsVerifiedDump.Should().BeFalse();
        }

        [Test]
        public void Should_parse_platform_tag_and_languages()
        {
            var result = _subject.Parse("Tekken 3 (USA) (En,Fr,De) [PS1].iso");

            result.Should().NotBeNull();
            result.GameTitle.Should().Be("Tekken 3");
            result.PlatformTag.Should().Be("PS1");
            result.Regions.Should().ContainSingle().Which.Should().Be("USA");
            result.Languages.Should().BeEquivalentTo("En", "Fr", "De");
            result.FormatExtension.Should().Be(".iso");
        }

        [Test]
        public void Should_return_null_for_null_or_whitespace_title()
        {
            _subject.Parse(null).Should().BeNull();
            _subject.Parse("   ").Should().BeNull();
        }

        [Test]
        public void Should_be_resolvable_from_container()
        {
            var container = new Container(rules => rules.WithNzbDroneRules());
            container.AutoAddServices(new List<string> { "Sonarr.Core" });

            var parser = container.Resolve<IRomReleaseParser>();
            parser.Should().NotBeNull();
            parser.Should().BeOfType<RomReleaseParser>();
        }
    }
}
