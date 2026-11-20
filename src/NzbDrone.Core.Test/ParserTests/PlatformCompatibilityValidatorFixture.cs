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
        [TestCase("Super.Mario.64.Virtual.Console.WAD", "wii", "Nintendo Wii", null, false)]
        [TestCase("Super.Mario.64.Virtual.Console.WAD", "wiiware", "Nintendo WiiWare", null, true)]
        [TestCase("Nintendo 64DD Complete Collection", "64dd", "Nintendo 64DD", null, true)]
        [TestCase("Virtual Boy - FlashBoy+ 2020-04-30.7z", "vb", "Virtual Boy", null, true)]
        [TestCase("Nintendo - Game Boy Color/Pokemon Crystal (USA).zip", "gbc", "Game Boy Color", null, true)]
        [TestCase("Nintendo - Game Boy Advance/Pokemon Emerald (USA).zip", "gba", "Game Boy Advance", null, true)]
        [TestCase("Sony - PlayStation 2/Gran Turismo 3 (USA).chd", "ps2", "Sony PlayStation 2", null, true)]
        [TestCase("Nintendo - Wii U/Mario Kart 8 (USA).wua", "wiiu", "Nintendo Wii U", null, true)]
        public void should_validate_platform_compatibility(string candidatePath, string platformSlug, string platformName, string format, bool expected)
        {
            var result = PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName, format);
            result.Should().Be(expected);
        }

        [TestCase("Pokemon Crystal (USA).zip", "gbc", "Game Boy Color", "nointro.gbc", true)]
        [TestCase("Pokemon Emerald (USA).zip", "gba", "Game Boy Advance", "nointro.gba", true)]
        [TestCase("Pokemon Red (USA).zip", "gba", "Game Boy Advance", "nointro.gb", false)]
        [TestCase("Virtual Boy - FlashBoy+ 2020-04-30.7z", "vb", "Virtual Boy", "htgdb-gamepacks", true)]
        [TestCase("Virtual Boy - FlashBoy+ 2020-04-30.7z", "lynx", "Atari Lynx", "htgdb-gamepacks", false)]
        public void should_validate_platform_compatibility_with_archive_id(string candidatePath, string platformSlug, string platformName, string archiveId, bool expected)
        {
            var result = PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName, null, archiveId);
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

        [TestCase("Super Mario Bros (PC Game).zip", "fds", "Famicom Disk System", false)]
        [TestCase("Doom (PC Image).zip", "n64", "Nintendo 64", false)]
        [TestCase("Doom - PC Image.zip", "n64", "Nintendo 64", false)]
        [TestCase("Doom PC Disk 1.zip", "n64", "Nintendo 64", false)]
        [TestCase("Quake [PC Disk].zip", "ps1", "PlayStation", false)]
        [TestCase("Duke Nukem (PC ISO).zip", "n64", "Nintendo 64", false)]
        [TestCase("Fallout (PC, 1997).zip", "ps1", "PlayStation", false)]
        [TestCase("Diablo for PC.zip", "ps1", "PlayStation", false)]
        [TestCase("Super Mario 64 (OST).zip", "n64", "Nintendo 64", false)]
        [TestCase("Zelda Ocarina of Time Strategy Guide.pdf", "n64", "Nintendo 64", false)]
        [TestCase("Zelda (PC).zip", "fds", "Famicom Disk System", false)]
        [TestCase("Castlevania [PC].zip", "nes", "Nintendo Entertainment System", false)]
        [TestCase("Doom (Windows).zip", "ps1", "PlayStation", false)]
        [TestCase("Prince of Persia (MS-DOS).zip", "snes", "Super Nintendo", false)]
        [TestCase("Super Mario Run (iOS).zip", "fds", "Famicom Disk System", false)]
        [TestCase("Castlevania - iOS Version.zip", "nes", "Nintendo Entertainment System", false)]
        [TestCase("Sonic the Hedgehog (Android).apk", "genesis", "Sega Genesis", false)]
        [TestCase("wiki-bloxcardswiki-20230101.zip", "fds", "Famicom Disk System", false)]
        [TestCase("My Game (Roblox).zip", "fds", "Famicom Disk System", false)]
        [TestCase("Castlevania Harmony of Despair (XBLA).zip", "fds", "Famicom Disk System", false)]
        [TestCase("Castlevania Harmony of Despair (XBLA).zip", "xbox360", "Xbox 360", true)]
        [TestCase("Bonk's Adventure (PC Engine) (USA).zip", "tg16", "TurboGrafx-16", true)]
        [TestCase("PC Kid (Japan).pce", "tg16", "TurboGrafx-16", true)]
        [TestCase("Zeroigar (PC-FX) (Japan).chd", "pcfx", "PC-FX", true)]
        [TestCase("Akumajou Dracula (Japan).fds", "fds", "Famicom Disk System", true)]
        public void should_reject_pc_and_non_console_releases(string candidatePath, string platformSlug, string platformName, bool expected)
        {
            var result = PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName);
            result.Should().Be(expected);
        }

        [Test]
        public void should_reject_zip_containing_incompatible_platform_rom_extension()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "controllarr-zip-compat-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);

            try
            {
                var a26Zip = System.IO.Path.Combine(tempDir, "Activision Decathlon (USA).zip");
                using (var archive = System.IO.Compression.ZipFile.Open(a26Zip, System.IO.Compression.ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Activision Decathlon (USA).a26");
                    using var writer = new System.IO.StreamWriter(entry.Open());
                    writer.Write("dummy");
                }

                var a78Zip = System.IO.Path.Combine(tempDir, "Food Fight (USA).zip");
                using (var archive = System.IO.Compression.ZipFile.Open(a78Zip, System.IO.Compression.ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Food Fight (USA).a78");
                    using var writer = new System.IO.StreamWriter(entry.Open());
                    writer.Write("dummy");
                }

                PlatformCompatibilityValidator.IsLocalFileCompatible(a26Zip, "atari7800", "Atari 7800").Should().BeFalse();
                PlatformCompatibilityValidator.IsLocalFileCompatible(a26Zip, "atari2600", "Atari 2600").Should().BeTrue();
                PlatformCompatibilityValidator.IsLocalFileCompatible(a78Zip, "atari7800", "Atari 7800").Should().BeTrue();
            }
            finally
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
