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
    }
}
