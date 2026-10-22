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
        public void Should_parse_nointro_standard()
        {
            var result = _subject.Parse("Legend of Zelda, The - A Link to the Past (USA).sfc");

            result.Should().NotBeNull();
            result.Title.Should().Be("Legend of Zelda, The - A Link to the Past");
            result.Regions.Should().ContainSingle().Which.Should().Be("USA");
            result.Format.Should().Be(".sfc");
            result.Platform.Should().Be("SNES"); // Decoded from extension

            var result2 = _subject.Parse("Sonic The Hedgehog (Japan, Korea) (En) (Rev A) [!].md");
            result2.Title.Should().Be("Sonic The Hedgehog");
            result2.Regions.Should().BeEquivalentTo("Japan", "Korea");
            result2.Languages.Should().BeEquivalentTo("En");
            result2.Revision.Should().Be("Rev A");
            result2.Flags.Should().Contain("!");
            result2.Format.Should().Be(".md");
        }

        [Test]
        public void Should_parse_redump_disc()
        {
            var result = _subject.Parse("Final Fantasy VII (USA) (Disc 1) (v1.1).chd");

            result.Should().NotBeNull();
            result.Title.Should().Be("Final Fantasy VII");
            result.Regions.Should().ContainSingle().Which.Should().Be("USA");
            result.Revision.Should().Be("v1.1");
            result.DiscNumber.Should().Be(1);
            result.DiscCount.Should().BeNull();
            result.Format.Should().Be(".chd");

            var result2 = _subject.Parse("Metal Gear Solid (USA) (Disc 1 of 2).iso");
            result2.Title.Should().Be("Metal Gear Solid");
            result2.DiscNumber.Should().Be(1);
            result2.DiscCount.Should().Be(2);
        }

        [Test]
        public void Should_parse_tosec_standard()
        {
            var result = _subject.Parse("Sonic the Hedgehog (1991)(Sega)(US)(Proto)");

            result.Should().NotBeNull();
            result.Title.Should().Be("Sonic the Hedgehog");
            result.Revision.Should().Be("Proto");
        }

        [Test]
        public void Should_parse_scene_release_groups_and_hashes()
        {
            var result = _subject.Parse("Super_Mario_Bros-SUXXORS [1A2B3C4D].nes");

            result.Should().NotBeNull();
            result.Title.Should().Be("Super_Mario_Bros");
            result.Platform.Should().Be("NES");
            result.Format.Should().Be(".nes");
        }

        [Test]
        public void Should_calculate_rom_priority()
        {
            var preferredRegions = new List<string> { "USA", "Europe" };

            var best = new RomReleaseInfo { Regions = new List<string> { "USA" }, Flags = new List<string> { "!" }, Revision = "Rev 1" };
            var bad = new RomReleaseInfo { Regions = new List<string> { "Japan" }, Flags = new List<string> { "b" } };
            var proto = new RomReleaseInfo { Regions = new List<string> { "USA" }, Revision = "Proto" };
            var noFlags = new RomReleaseInfo { Regions = new List<string> { "USA" } };
            var eur = new RomReleaseInfo { Regions = new List<string> { "Europe" }, Flags = new List<string> { "!" } };

            var bestScore = _subject.CalculateRomPriority(best, preferredRegions);
            var badScore = _subject.CalculateRomPriority(bad, preferredRegions);
            var protoScore = _subject.CalculateRomPriority(proto, preferredRegions);
            var noFlagsScore = _subject.CalculateRomPriority(noFlags, preferredRegions);
            var eurScore = _subject.CalculateRomPriority(eur, preferredRegions);

            bestScore.Should().BeGreaterThan(noFlagsScore);
            noFlagsScore.Should().BeGreaterThan(protoScore);
            bestScore.Should().BeGreaterThan(eurScore);
            eurScore.Should().BeGreaterThan(badScore);
        }

        [Test]
        public void Should_parse_multi_language()
        {
            var result = _subject.Parse("Pokemon - Emerald Version (Europe) (En,Fr,De,Es,It).gba");

            result.Should().NotBeNull();
            result.Title.Should().Be("Pokemon - Emerald Version");
            result.Platform.Should().Be("GBA");
            result.Regions.Should().ContainSingle().Which.Should().Be("Europe");
            result.Languages.Should().BeEquivalentTo("En", "Fr", "De", "Es", "It");
            result.Format.Should().Be(".gba");
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
