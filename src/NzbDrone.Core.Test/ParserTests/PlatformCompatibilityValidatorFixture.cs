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
        [TestCase("Kill Switch (USA) (PS2).iso", "ps2", "Sony PlayStation 2", null, true)]
        [TestCase("Batman Begins (DC Comics) (PS2).iso", "ps2", "Sony PlayStation 2", null, true)]
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
        [TestCase("Persona S1 Edition (USA).bin", false)]
        [TestCase("Racing S2 Championship (Europe).iso", false)]
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
        [TestCase("Castlevania Harmony of Despair (XBLA).zip", "xbox360digital", "Xbox 360 Digital", true)]
        [TestCase("Castlevania Harmony of Despair (XBLA).zip", "xbox360", "Xbox 360", false)]
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

        [TestCase("SNK - Neo Geo CD - Metal Slug.chd", "neogeocd", "neogeo")]
        [TestCase("NEC - PC Engine SuperGrafx - Aldynes.sgx", "supergrafx", "tg16")]
        [TestCase("Microsoft - Xbox 360 (Digital) - Braid (XBLA).zip", "xbox360digital", "xbox360")]
        [TestCase("Sony - PlayStation 3 (PSN) - Journey.pkg", "ps3psn", "ps3")]
        [TestCase("Sony - PlayStation Portable (PSN) - Patapon.pkg", "psppsn", "psp")]
        [TestCase("Philips - Videopac+ - Pick Axe Pete+.bin", "videopacplus", "odyssey2")]
        [TestCase("Epoch - Super Cassette Vision - Lupin III.bin", "scv", "cassettevision")]
        [TestCase("Bandai - WonderSwan Color - Final Fantasy.wsc", "wonderswan", "ws")]
        [TestCase("SNK - Neo Geo Pocket Color - Match.ngc", "ngpc", "ngp")]
        [TestCase("Sega - Beena - Anpanman.bin", "beena", "pico")]
        [TestCase("Nintendo - e-Reader - Mario Party-e.raw", "ereader", "gba")]
        [TestCase("Bandai - Sufami Turbo - Ultraman.st", "sufamiturbo", "snes")]
        public void should_distinguish_sibling_and_new_console_platforms_on_detection(string input, string expectedSlug, string excludedParentSlug)
        {
            var detected = PlatformCompatibilityValidator.DetectPlatforms(input);
            detected.Should().Contain(expectedSlug);
            detected.Should().NotContain(excludedParentSlug);
        }

        [Test]
        public void should_allow_shared_rom_extensions_for_new_platforms_when_untagged()
        {
            PlatformCompatibilityValidator.IsCompatible("Sega Pico/Sonic Gameworld (USA).md", "pico", "Sega Pico").Should().BeTrue();
            PlatformCompatibilityValidator.IsCompatible("Sega Pico/Sonic (Genesis).md", "pico", "Sega Pico").Should().BeFalse();
            PlatformCompatibilityValidator.IsCompatible("GamePark GP32/Astonishia Story R (Korea).smc", "gp32", "GamePark GP32").Should().BeTrue();
            PlatformCompatibilityValidator.IsCompatible("GamePark GP32/Chrono Trigger (SNES).smc", "gp32", "GamePark GP32").Should().BeFalse();
        }

        [TestCase("jaguarcd", "Atari Jaguar CD")]
        [TestCase("jagcd", "Atari Jaguar CD")]
        [TestCase("jaguar", "Atari Jaguar")]
        [TestCase("64dd", "Nintendo 64DD")]
        [TestCase("n64dd", "Nintendo 64DD")]
        [TestCase("n64", "Nintendo 64")]
        [TestCase("satellaview", "Nintendo Satellaview")]
        [TestCase("sufamiturbo", "Bandai Sufami Turbo")]
        [TestCase("fds", "Nintendo Famicom Disk System")]
        [TestCase("tgcd", "NEC TurboGrafx-CD")]
        [TestCase("supergrafx", "NEC PC Engine SuperGrafx")]
        [TestCase("ereader", "Nintendo e-Reader")]
        [TestCase("videopacplus", "Philips Videopac+ G7400")]
        public void should_resolve_addon_platform_names_independently(string input, string expectedCanonicalName)
        {
            PlatformCompatibilityValidator.ResolvePlatformName(input).Should().Be(expectedCanonicalName);
        }

        [TestCase("jaguarcd", "jaguar", false)]
        [TestCase("jaguar", "jaguarcd", false)]
        [TestCase("64dd", "n64", false)]
        [TestCase("n64", "64dd", false)]
        [TestCase("fds", "nes", false)]
        [TestCase("nes", "fds", false)]
        [TestCase("satellaview", "snes", false)]
        [TestCase("snes", "satellaview", false)]
        [TestCase("sufamiturbo", "snes", false)]
        [TestCase("tgcd", "tg16", false)]
        [TestCase("pcecd", "pce", false)]
        [TestCase("ereader", "gba", false)]
        [TestCase("wiiware", "wii", false)]
        [TestCase("wii", "wiiware", false)]
        [TestCase("wiiueshop", "wiiu", false)]
        [TestCase("wiiu", "wiiueshop", false)]
        [TestCase("dsiware", "nds", false)]
        [TestCase("nds", "dsiware", false)]
        [TestCase("3dseshop", "3ds", false)]
        [TestCase("3ds", "3dseshop", false)]
        [TestCase("switcheshop", "switch", false)]
        [TestCase("switch", "switcheshop", false)]
        [TestCase("xbox360digital", "xbox360", false)]
        [TestCase("xbox360", "xbox360digital", false)]
        [TestCase("ps3psn", "ps3", false)]
        [TestCase("ps3", "ps3psn", false)]
        [TestCase("psppsn", "psp", false)]
        [TestCase("psp", "psppsn", false)]
        [TestCase("pce", "tg16", true)]
        [TestCase("tg16", "pce", true)]
        public void should_not_cross_match_addon_or_digital_platforms_with_parent(string detectedSlug, string targetSlug, bool expected)
        {
            PlatformCompatibilityValidator.IsPlatformMatch(detectedSlug, targetSlug).Should().Be(expected);
        }

        [TestCase("Atari Jaguar CD/Battlemorph (USA).chd", "jaguarcd", "Atari Jaguar CD", true)]
        [TestCase("Atari Jaguar CD/Battlemorph (USA).chd", "jaguar", "Atari Jaguar", false)]
        [TestCase("Atari Jaguar/Tempest 2000 (USA).j64", "jaguar", "Atari Jaguar", true)]
        [TestCase("Atari Jaguar/Tempest 2000 (USA).j64", "jaguarcd", "Atari Jaguar CD", false)]
        [TestCase("Nintendo 64DD/F-Zero X Expansion Kit (Japan).ndd", "64dd", "Nintendo 64DD", true)]
        [TestCase("Nintendo 64DD/F-Zero X Expansion Kit (Japan).ndd", "n64", "Nintendo 64", false)]
        [TestCase("Nintendo 64/Super Mario 64 (USA).z64", "n64", "Nintendo 64", true)]
        [TestCase("Nintendo 64/Super Mario 64 (USA).z64", "64dd", "Nintendo 64DD", false)]
        [TestCase("Famicom Disk System/Metroid (Japan).fds", "fds", "Nintendo Famicom Disk System", true)]
        [TestCase("Famicom Disk System/Metroid (Japan).fds", "nes", "Nintendo Entertainment System", false)]
        [TestCase("NES/Super Mario Bros (USA).nes", "fds", "Nintendo Famicom Disk System", false)]
        [TestCase("Satellaview/BS Zelda no Densetsu (Japan).bs", "satellaview", "Nintendo Satellaview", true)]
        [TestCase("Satellaview/BS Zelda no Densetsu (Japan).bs", "snes", "Super Nintendo Entertainment System", false)]
        [TestCase("Sufami Turbo/SD Ultra Battle (Japan).st", "sufamiturbo", "Bandai Sufami Turbo", true)]
        [TestCase("Sufami Turbo/SD Ultra Battle (Japan).st", "snes", "Super Nintendo Entertainment System", false)]
        [TestCase("SNES/Chrono Trigger (USA).sfc", "sufamiturbo", "Bandai Sufami Turbo", false)]
        [TestCase("TurboGrafx-CD/Rondo of Blood (Japan).chd", "tgcd", "NEC TurboGrafx-CD", true)]
        [TestCase("TurboGrafx-CD/Rondo of Blood (Japan).chd", "tg16", "NEC TurboGrafx-16", false)]
        [TestCase("Nintendo e-Reader/Mario Party-e (USA).raw", "ereader", "Nintendo e-Reader", true)]
        [TestCase("Nintendo e-Reader/Mario Party-e (USA).raw", "gba", "Game Boy Advance", false)]
        [TestCase("GBA/Metroid Fusion (USA).gba", "ereader", "Nintendo e-Reader", false)]
        public void should_isolate_addon_formats_and_paths_from_parent_platform(string candidatePath, string platformSlug, string platformName, bool expected)
        {
            PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName).Should().Be(expected);
        }

        [TestCase("TG16 CD/Lords of Thunder (USA).chd")]
        [TestCase("TG16-CD/Lords of Thunder (USA).chd")]
        [TestCase("TurboGrafx-16 CD/Ys Book I & II (USA).chd")]
        [TestCase("TGFX16-CD/Gate of Thunder (USA).chd")]
        [TestCase("PCE CD/Akumajou Dracula X (Japan).chd")]
        [TestCase("PC Engine Super CD-ROM2/Rondo of Blood (Japan).chd")]
        public void should_detect_tgcd_variants_and_allow_chd_without_bleeding_to_tg16(string candidatePath)
        {
            var detected = PlatformCompatibilityValidator.DetectPlatforms(candidatePath);
            detected.Should().Contain("tgcd");
            detected.Should().NotContain("tg16");
            PlatformCompatibilityValidator.IsLocalFileCompatible(candidatePath, "tgcd", "NEC TurboGrafx-CD").Should().BeTrue();
        }

        [TestCase("The Addams Family (tv series)", true)]
        [TestCase("The Addams Family (tv show)", true)]
        [TestCase("The Addams Family (USA).chd", false)]
        public void should_detect_tv_series_and_show_tags_for_zero_tv_rejection(string releaseTitle, bool expected)
        {
            PlatformCompatibilityValidator.IsTvOrVideoRelease(releaseTitle).Should().Be(expected);
        }

        [TestCase("The Addams Family (Atari ST)", true)]
        [TestCase("The Hunt for Red October (Atari ST)", true)]
        [TestCase("Superfrog (Amiga)", true)]
        [TestCase("Last Ninja (Commodore 64)", true)]
        [TestCase("Superfrog (Amiga CD32)", false)]
        [TestCase("The Apprentice (USA) (Philips CD-i)", false)]
        public void should_reject_non_console_retro_computers_while_preserving_amiga_cd32(string releaseTitle, bool expected)
        {
            PlatformCompatibilityValidator.IsPcOrNonConsoleRelease(releaseTitle).Should().Be(expected);
        }
    }
}
