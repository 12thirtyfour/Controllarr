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
        [TestCase("Mega Man 2 (USA).exe", "nes", "Nintendo Entertainment System", null, false)]
        [TestCase("setup_mega_man_2_2.0.0.4.exe", "nes", "Nintendo Entertainment System", null, false)]
        [TestCase("Super Mario Bros (USA) (Official GOG Installer).zip", "nes", "Nintendo Entertainment System", null, false)]
        [TestCase("Super Mario Bros (USA).nes", "nes", "Nintendo Entertainment System", null, true)]
        [TestCase("Super Mario Galaxy (Wii).iso", "switch", "Nintendo Switch", null, false)]
        [TestCase("Super Mario Galaxy (Wii).wbfs", "switch", "Nintendo Switch", null, false)]
        [TestCase("Super.Mario.Galaxy.USA.WII-SUSHi", "switch", "Nintendo Switch", null, false)]
        [TestCase("Super Mario Galaxy (Nintendo Wii)", "switch", "Nintendo Switch", null, false)]
        [TestCase("Super Mario Galaxy [Wii]", "switch", "Nintendo Switch", null, false)]
        [TestCase("Super Mario Galaxy (USA).zip", "switch", "Nintendo Switch", null, true)]
        [TestCase("Super Mario Galaxy (Switch)", "switch", "Nintendo Switch", null, true)]
        [TestCase("Super Mario Galaxy [Nintendo Switch].nsp", "switch", "Nintendo Switch", null, true)]
        [TestCase("Super Mario Galaxy [Nintendo Switch].xci", "switch", "Nintendo Switch", null, true)]
        [TestCase("Super Mario Galaxy (Wii).iso", "wii", "Nintendo Wii", null, true)]
        [TestCase("Super Mario Galaxy (Switch)", "wii", "Nintendo Wii", null, false)]
        [TestCase("Sonic the Hedgehog (Genesis).zip", "sms", "Sega Master System", null, false)]
        [TestCase("Sonic the Hedgehog (SMS).zip", "sms", "Sega Master System", null, true)]
        [TestCase("Sonic the Hedgehog.zip", "sms", "Sega Master System", null, true)]
        [TestCase("Halo 2 (Xbox).iso", "gba", "Game Boy Advance", null, false)]
        [TestCase("Halo.2.Xbox.ISO-GROUP", "gba", "Game Boy Advance", null, false)]
        [TestCase("Some.Xbox.Game.GBA.Emulator.iso", "gba", "Game Boy Advance", null, false)]
        [TestCase("Pokemon - FireRed Version (USA).gba", "gba", "Game Boy Advance", null, true)]
        [TestCase("Wii Sports (Wii).iso", "gba", "Game Boy Advance", null, false)]
        [TestCase("Mario Kart 64 (USA).z64", "n64", "Nintendo 64", null, true)]
        [TestCase("Mario Kart Double Dash.iso", "n64", "Nintendo 64", null, false)]
        [TestCase("Halo.XBOXDVD-GROUP", "atari7800", "Atari 7800", null, false)]
        [TestCase("Halo.XBOXDVD-GROUP", "xbox", "Microsoft Xbox", null, true)]
        [TestCase("Final.Fantasy.VII.PSX2PSP-GROUP", "atari7800", "Atari 7800", null, false)]
        [TestCase("Final.Fantasy.VII.PSX2PSP-GROUP", "psp", "Sony PSP", null, true)]
        [TestCase("Super.Mario.64.Virtual.Console.WAD", "n64", "Nintendo 64", null, false)]
        [TestCase("Super.Mario.64.Virtual.Console.WAD", "wii", "Nintendo Wii", null, true)]
        [TestCase("Nintendo 64DD Complete Collection", "64dd", "Nintendo 64DD", null, true)]
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
