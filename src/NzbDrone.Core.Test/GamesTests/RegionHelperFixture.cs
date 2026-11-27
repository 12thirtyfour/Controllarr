using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class RegionHelperFixture
    {
        [TestCase("japan-j3")]
        [TestCase("sp-1v1_3db8c")]
        [TestCase("sp1-j3")]
        [TestCase("sp1-u3")]
        [TestCase("sp1-u4")]
        [TestCase("srom")]
        [TestCase("sp1-u4.bin")]
        public void should_detect_neogeo_bios_names(string name)
        {
            RegionHelper.IsBios(name).Should().BeTrue();
        }

        [TestCase("Metal Slug")]
        [TestCase("King of Fighters '98")]
        public void should_not_flag_regular_neogeo_games_as_bios(string name)
        {
            RegionHelper.IsBios(name).Should().BeFalse();
        }

        [TestCase("Atari 5200 BIOS (USA)")]
        [TestCase("Nintendo DS Firmware (World)")]
        [TestCase("Game Boy BootROM (World)")]
        [TestCase("Atari Lynx Boot Image (World)")]
        [TestCase("Nintendo 64DD IPL (Japan)")]
        [TestCase("GameCube IPL (USA)")]
        [TestCase("Super CD-ROM2 System (Japan)")]
        [TestCase("Wii System Menu (USA)")]
        [TestCase("scph5501.bin")]
        [TestCase("gba_bios.bin")]
        [TestCase("bios7.bin")]
        [TestCase("firmware.bin")]
        [TestCase("dc_boot.bin")]
        [TestCase("syscard3.pce")]
        [TestCase("lynxboot.img")]
        [TestCase("exec.bin")]
        [TestCase("grom.bin")]
        [TestCase("kick40060.cd32")]
        public void should_detect_console_bios_firmware_and_boot_roms(string name)
        {
            RegionHelper.IsBios(name).Should().BeTrue();
        }

        [TestCase("Bioshock (USA)")]
        [TestCase("Bionic Commando (USA)")]
        [TestCase("Bio-Hazard Battle (USA)")]
        [TestCase("Bio Force Ape (Japan)")]
        [TestCase("Das Boot (Europe)")]
        [TestCase("Boot Camp (USA)")]
        [TestCase("Puss in Boots (USA)")]
        [TestCase("ReBoot (USA)")]
        [TestCase("System Shock (USA)")]
        [TestCase("Kick Off (Europe)")]
        [TestCase("Mine Storm (World)")]
        [TestCase("Sonic CD (USA)")]
        [TestCase("Super Mario Bros. (World)")]
        public void should_not_flag_retail_games_with_similar_words_as_bios(string name)
        {
            RegionHelper.IsBios(name).Should().BeFalse();
        }

        [TestCase("Mother 3 (Japan) [T+Eng1.2]")]
        [TestCase("Seiken Densetsu 3 (Japan) [T-En by Neill Corlett]")]
        [TestCase("Asterix (Europe)[tr pt Guto]")]
        [TestCase("Booby Boys (english translation)")]
        [TestCase("Chrono Trigger (Japan) (Retranslation)")]
        public void should_classify_translations_separately_from_rom_hacks(string name)
        {
            RegionHelper.IsTranslation(name).Should().BeTrue();
            RegionHelper.GetRegionName(9).Should().Be("Translations");
            RegionHelper.GetRegionName(6).Should().Be("ROM Hacks");
            RegionHelper.GetRegionNumber("Translations").Should().Be(9);
        }

        [TestCase("Super Mario World (USA) (Hack)")]
        [TestCase("Pokemon Brown (USA) (Color Hack)")]
        [TestCase("Tetris (World)[h1]")]
        [TestCase("Addams Family (USA)[t]")]
        public void should_not_flag_non_translation_rom_hacks_as_translations(string name)
        {
            RegionHelper.IsTranslation(name).Should().BeFalse();
            RegionHelper.IsRomHackOrTranslation(name).Should().BeTrue();
        }

        [TestCase("Game Boy Aging Cartridge (World) (En,Ja) (v1.1)", true)]
        [TestCase("Super Famicom Aging Program Ver. 1.00 (Japan) (En)", true)]
        [TestCase("SNSP Aging Ver 1.02 (Japan) (En)", true)]
        [TestCase("Controller Test Cassette (Japan) (En)", true)]
        [TestCase("Super NES Control Deck Tester (Japan) (En) (Rev 1)", true)]
        [TestCase("Arkagis Revolution - Input Diagnostics (World)", true)]
        [TestCase("MD Soft Checker (USA) (Proto)", true)]
        [TestCase("SoundTest 2 (World) (Program) (Aftermarket) (Unl)", true)]
        [TestCase("Gesture Test for Power Pad (World) (Program) (Aftermarket) (Unl)", true)]
        [TestCase("OptImage CD-I 220 Adjustment Disc (USA)", true)]
        [TestCase("Test Drive II - The Duel (USA)", false)]
        [TestCase("Chinese Checkers (USA)", false)]
        [TestCase("Burn-Cycle (USA, Europe) (Disc 1) (The Game)", false)]
        public void should_classify_test_and_diagnostic_carts_across_platforms(string name, bool expected)
        {
            RegionHelper.IsTestOrUtility(name).Should().Be(expected);
        }

        [TestCase("Accent Health - April 2003 50 + Silver CDi 0403 (USA)", true)]
        [TestCase("CD-i Music Book - Classical Guitar Volume 1 (USA)", true)]
        [TestCase("Felix the Cat's Giant Electronic Comic Book (USA, Europe)", true)]
        [TestCase("Compton's Interactive Encyclopedia (USA)", false)]
        [TestCase("Rand McNally's America - United States Atlas (USA)", true)]
        [TestCase("CD-i Title Sampler - Fall '94 (USA)", true)]
        [TestCase("Time Life Presents - 35mm Photography (USA)", true)]
        [TestCase("Time-Life Astrology (USA)", true)]
        [TestCase("Louis Armstrong - An American Songbook (USA)", true)]
        [TestCase("Bell Atlantic Info-Active Electronic Directory (USA)", true)]
        [TestCase("Philips CD-i Product Preview (USA)", true)]
        [TestCase("Hotel Mario (USA)", false)]
        [TestCase("Zelda - The Wand of Gamelon (USA)", false)]
        [TestCase("Ys Book I & II (USA)", false)]
        [TestCase("Jungle Book, The (USA)", false)]
        [TestCase("Toy Story (USA)", false)]
        public void should_classify_cdi_magazines_and_reference_books_out_of_retail(string name, bool expected)
        {
            RegionHelper.IsMagazineOrBook(name).Should().Be(expected);
        }
    }
}
