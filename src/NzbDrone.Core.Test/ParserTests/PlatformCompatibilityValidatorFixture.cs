using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class PlatformCompatibilityValidatorFixture : CoreTest
    {
        [TestCase("Pokemon Ultra Sun USA 3DS-SUXXORS.3ds", "nintendo-3ds", "Nintendo 3DS", null, true)]
        [TestCase("Pokemon Ultra Sun USA 3DS-SUXXORS.cia", "nintendo-3ds", "Nintendo 3DS", null, true)]
        [TestCase("Pokemon Ultra Sun.sfc", "nintendo-3ds", "Nintendo 3DS", null, false)]
        [TestCase("Pokemon Ultra Sun.iso", "nintendo-3ds", "Nintendo 3DS", null, false)]
        [TestCase("Super Mario World.sfc", "super-nintendo-entertainment-system", "Super Nintendo Entertainment System", null, true)]
        [TestCase("Super Mario World.smc", "snes", "Super Nintendo", null, true)]
        [TestCase("Super Mario World.iso", "snes", "Super Nintendo", null, false)]
        [TestCase("Super Mario World.chd", "snes", "Super Nintendo", null, false)]
        [TestCase("Metal Gear Solid (Disc 1).chd", "sony-playstation", "Sony PlayStation", null, true)]
        [TestCase("Metal Gear Solid.iso", "psx", "PlayStation", null, true)]
        [TestCase("Metal Gear Solid.sfc", "psx", "PlayStation", null, false)]
        [TestCase("Sonic the Hedgehog.md", "sega-genesis", "Sega Genesis", null, true)]
        [TestCase("Sonic the Hedgehog.bin", "sega-genesis", "Sega Genesis", null, true)]
        [TestCase("Sonic the Hedgehog.z64", "sega-genesis", "Sega Genesis", null, false)]
        [TestCase("GBA/Pokemon Emerald.zip", "game-boy-advance", "Game Boy Advance", null, true)]
        [TestCase("SNES/Super Metroid.zip", "game-boy-advance", "Game Boy Advance", null, false)]
        [TestCase("archive.zip/3DS/game.3ds", "3ds", "Nintendo 3DS", null, true)]
        [TestCase("archive.zip/GBA/game.gba", "3ds", "Nintendo 3DS", null, false)]
        [TestCase("Pokemon Red.gb", "game-boy", "Game Boy", null, true)]
        [TestCase("Pokemon Crystal.gbc", "game-boy-color", "Game Boy Color", null, true)]
        [TestCase("Pokemon Crystal.gbc", "game-boy", "Game Boy", null, false)]
        public void should_validate_platform_compatibility(string candidatePath, string platformSlug, string platformName, string format, bool expected)
        {
            var result = PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName, format);
            result.Should().Be(expected);
        }

        [TestCase("The.Office.S03E115.DVDRip.XviD-OSiTV", true)]
        [TestCase("Breaking.Bad.S05E16.720p.HDTV.x264", true)]
        [TestCase("Game.Of.Thrones.Season.1.Complete.1080p", true)]
        [TestCase("Movie.Title.2023.1080p.BluRay.x264.mkv", true)]
        [TestCase("Super Mario Bros (USA).nes", false)]
        [TestCase("Chrono Trigger (USA).sfc", false)]
        [TestCase("Pokemon - Emerald Version (USA, Europe).gba", false)]
        [TestCase("Metal Gear Solid (USA) (Disc 1).chd", false)]
        public void should_identify_tv_or_video_releases(string releaseTitle, bool expected)
        {
            var result = PlatformCompatibilityValidator.IsTvOrVideoRelease(releaseTitle);
            result.Should().Be(expected);
        }
    }
}
