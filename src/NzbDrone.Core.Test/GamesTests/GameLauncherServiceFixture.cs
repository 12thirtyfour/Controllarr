using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games.Launcher;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class GameLauncherServiceFixture
    {
        private GameLauncherService _subject;

        [SetUp]
        public void Setup()
        {
            _subject = new GameLauncherService();
        }

        [TestCase("SNES", "snes9x", true)]
        [TestCase("Game Boy Advance", "mgba", true)]
        [TestCase("GameCube", "dolphin", false)]
        public void should_build_launch_descriptor_with_desktop_protocol_and_webplayer_url(
            string platformFolder,
            string expectedCore,
            bool expectedWasm)
        {
            var descriptor = _subject.BuildLaunchDescriptor(42, platformFolder, "/roms/SNES/Super Metroid.sfc");

            descriptor.GameId.Should().Be(42);
            descriptor.LibretroCore.Should().Be(expectedCore);
            descriptor.SupportsWebAssembly.Should().Be(expectedWasm);
            descriptor.DesktopProtocolUri.Should().StartWith($"controllarr://play/42?core={expectedCore}&rom=");
            descriptor.WebPlayerUrl.Should().StartWith($"/webplayer?gameId=42&core={expectedCore}&rom=");
        }

        [Test]
        public void should_allow_custom_libretro_core_overrides_and_settings()
        {
            var config = new LibretroLauncherConfig
            {
                EnableWebPlayer = true,
                PreferredLaunchMode = "retroarch",
                DefaultShader = "crt-mattias",
                CoreOverrides = new Dictionary<string, string>
                {
                    { "SNES", "bsnes" },
                    { "PlayStation", "swanstation" }
                }
            };

            _subject.SaveConfig(config);

            _subject.ResolveLibretroCore("SNES").Should().Be("bsnes");
            _subject.ResolveLibretroCore("Sony PlayStation").Should().Be("swanstation");

            var descriptor = _subject.BuildLaunchDescriptor(99, "SNES", "/roms/SNES/Chrono Trigger.sfc");
            descriptor.LibretroCore.Should().Be("bsnes");
            descriptor.PreferredLaunchMode.Should().Be("retroarch");
            descriptor.Shader.Should().Be("crt-mattias");

            var coreOptions = _subject.GetPlatformCoreOptions();
            coreOptions.Should().NotBeEmpty();
            coreOptions.Should().Contain(o => o.PlatformKey == "SNES" && o.SelectedCore == "bsnes");
        }

        [Test]
        public void should_extract_inner_rom_from_zip_archive_for_web_player_streaming()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_launcher_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var zipPath = Path.Combine(tempDir, "Tetris (World).zip");
                var expectedBytes = Encoding.ASCII.GetBytes("NINTENDO_GB_ROM_DATA");

                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Tetris (World).gb");
                    using var entryStream = entry.Open();
                    entryStream.Write(expectedBytes, 0, expectedBytes.Length);
                }

                var descriptor = _subject.BuildLaunchDescriptor(7, "gb", zipPath);
                descriptor.EmulatorJsCore.Should().Be("gb");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/7/rom/Tetris%20%28World%29.gb");

                using var stream = _subject.OpenRomStream(7, out var fileName, "gb", zipPath);
                stream.Should().NotBeNull();
                fileName.Should().Be("Tetris (World).gb");

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.ToArray().Should().Equal(expectedBytes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestCase("NES", "nes")]
        [TestCase("SNES", "snes")]
        [TestCase("Game Boy", "gb")]
        [TestCase("Game Boy Color", "gb")]
        [TestCase("Game Boy Advance", "gba")]
        [TestCase("Virtual Boy", "vb")]
        [TestCase("Nintendo 64", "n64")]
        [TestCase("64dd", "parallel_n64")]
        [TestCase("Nintendo DS", "nds")]
        [TestCase("Genesis", "segaMD")]
        [TestCase("Master System", "segaMS")]
        [TestCase("sg1000", "genesis_plus_gx")]
        [TestCase("Game Gear", "segaGG")]
        [TestCase("segacd", "segaCD")]
        [TestCase("sega32x", "sega32x")]
        [TestCase("saturn", "segaSaturn")]
        [TestCase("psx", "psx")]
        [TestCase("tg16", "pce")]
        [TestCase("tgcd", "pce")]
        [TestCase("supergrafx", "pce")]
        [TestCase("neogeo", "arcade")]
        [TestCase("ngp", "ngp")]
        [TestCase("ws", "ws")]
        [TestCase("pocketchallengev2", "ws")]
        [TestCase("atari2600", "atari2600")]
        [TestCase("atari5200", "atari5200")]
        [TestCase("atari7800", "atari7800")]
        [TestCase("lynx", "lynx")]
        [TestCase("jaguar", "jaguar")]
        [TestCase("coleco", "coleco")]
        [TestCase("3do", "3do")]
        [TestCase("pcfx", "pcfx")]
        [TestCase("cdi", "same_cdi")]
        [TestCase("cd32", "amiga")]
        public void should_map_all_supported_wasm_consoles_to_emulatorjs_cores(string platform, string expectedEjsCore)
        {
            var descriptor = _subject.BuildLaunchDescriptor(100, platform, $"/roms/{platform}/Game.bin");

            descriptor.SupportsWebAssembly.Should().BeTrue();
            descriptor.EmulatorJsCore.Should().Be(expectedEjsCore);
        }

        [Test]
        public void should_resolve_segacd_bios_and_keep_cue_bin_zip_intact()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_segacd_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                var biosPath = Path.Combine(biosDir, "bios_CD_U.bin");
                File.WriteAllBytes(biosPath, new byte[] { 1, 2, 3, 4 });

                var zipPath = Path.Combine(tempDir, "Sonic CD (USA).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var cueEntry = archive.CreateEntry("Sonic CD (USA).cue");
                    using (var s = cueEntry.Open())
                    {
                        var bytes = Encoding.ASCII.GetBytes("FILE \"Sonic CD (USA) (Track 01).bin\" BINARY\n");
                        s.Write(bytes, 0, bytes.Length);
                    }

                    var binEntry = archive.CreateEntry("Sonic CD (USA) (Track 01).bin");
                    using (var s = binEntry.Open())
                    {
                        s.Write(new byte[] { 0xAA, 0xBB }, 0, 2);
                    }
                }

                var descriptor = _subject.BuildLaunchDescriptor(55, "segacd", zipPath);
                descriptor.EmulatorJsCore.Should().Be("segaCD");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/55/bios/bios_CD_U.bin");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/55/rom/Sonic%20CD%20%28USA%29.zip");

                using var biosStream = _subject.OpenBiosStream(55, out var biosFileName, "segacd", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("bios_CD_U.bin");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_resolve_fds_bios_for_zipped_fds_rom()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_fds_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                var biosPath = Path.Combine(biosDir, "disksys.rom");
                File.WriteAllBytes(biosPath, new byte[] { 1, 2, 3, 4 });

                var zipPath = Path.Combine(tempDir, "Super Mario Bros. 2 (Japan).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var fdsEntry = archive.CreateEntry("Super Mario Bros. 2 (Japan).fds");
                    using var s = fdsEntry.Open();
                    s.Write(new byte[] { 0x46, 0x44, 0x53, 0x1A }, 0, 4);
                }

                var descriptor = _subject.BuildLaunchDescriptor(77, "Nintendo - Family Computer Disk System", zipPath);
                descriptor.EmulatorJsCore.Should().Be("nes");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/77/bios/disksys.rom");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/77/rom/Super%20Mario%20Bros.%202%20%28Japan%29.fds");

                using var biosStream = _subject.OpenBiosStream(77, out var biosFileName, "Nintendo - Family Computer Disk System", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("disksys.rom");

                using var romStream = _subject.OpenRomStream(77, out var romFileName, "Nintendo - Family Computer Disk System", zipPath);
                romStream.Should().NotBeNull();
                romFileName.Should().Be("Super Mario Bros. 2 (Japan).fds");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_extract_64dd_ndd_from_zip_and_resolve_ipl_bios_url()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_64dd_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                var biosPath = Path.Combine(biosDir, "64DD_IPL.bin");
                File.WriteAllBytes(biosPath, new byte[] { 1, 2, 3, 4 });

                var zipPath = Path.Combine(tempDir, "F-Zero X Expansion Kit (Japan).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var nddEntry = archive.CreateEntry("F-Zero X Expansion Kit (Japan).ndd");
                    using var s = nddEntry.Open();
                    s.Write(new byte[] { 0xE8, 0x48, 0xD3, 0x16 }, 0, 4);
                }

                var descriptor = _subject.BuildLaunchDescriptor(88, "Nintendo - Nintendo 64DD", zipPath);
                descriptor.LibretroCore.Should().Be("parallel_n64");
                descriptor.EmulatorJsCore.Should().Be("parallel_n64");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/88/bios/64DD_IPL.bin");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/88/rom/F-Zero%20X%20Expansion%20Kit%20%28Japan%29.ndd");

                using var biosStream = _subject.OpenBiosStream(88, out var biosFileName, "Nintendo - Nintendo 64DD", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("64DD_IPL.bin");

                using var romStream = _subject.OpenRomStream(88, out var romFileName, "Nintendo - Nintendo 64DD", zipPath);
                romStream.Should().NotBeNull();
                romFileName.Should().Be("F-Zero X Expansion Kit (Japan).ndd");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_block_rom_stream_outside_configured_library_folders()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_launcher_confinement_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var platformDir = Path.Combine(tempDir, "Library", "SNES");
                Directory.CreateDirectory(platformDir);

                var validRomPath = Path.Combine(platformDir, "Chrono Trigger (USA).sfc");
                var secretFilePath = Path.Combine(tempDir, "config.xml");
                File.WriteAllBytes(validRomPath, Encoding.ASCII.GetBytes("SNES_ROM"));
                File.WriteAllText(secretFilePath, "<Config><ApiKey>secret</ApiKey></Config>");

                var seriesServiceMock = new Mock<ISeriesService>();
                seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
                {
                    new Series { Id = 1, Title = "Super Nintendo", TitleSlug = "snes", Path = platformDir }
                });

                var confinedSubject = new GameLauncherService(seriesService: seriesServiceMock.Object);

                confinedSubject.OpenRomStream(0, out _, "snes", secretFilePath).Should().BeNull();

                using var allowedStream = confinedSubject.OpenRomStream(0, out var fileName, "snes", validRomPath);
                allowedStream.Should().NotBeNull();
                fileName.Should().Be("Chrono Trigger (USA).sfc");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestCase("jaguarcd")]
        [TestCase("Jaguar CD")]
        [TestCase("Atari - Jaguar CD")]
        [TestCase("Atari Jaguar CD")]
        public void should_mark_jaguar_cd_as_desktop_only_and_preserve_cartridge_jaguar_wasm(string platform)
        {
            var cdDescriptor = _subject.BuildLaunchDescriptor(120, platform, $"/roms/{platform}/Blue Lightning (World).cue");

            cdDescriptor.LibretroCore.Should().Be("virtualjaguar");
            cdDescriptor.EmulatorJsCore.Should().Be("jaguarcd");
            cdDescriptor.SupportsWebAssembly.Should().BeFalse();
            cdDescriptor.WebAssemblyUnsupportedReason.Should().Contain("Desktop RetroArch");
            cdDescriptor.WebAssemblyUnsupportedReason.Should().Contain("BigPEmu");

            var cartDescriptor = _subject.BuildLaunchDescriptor(121, "Atari Jaguar", "/roms/jaguar/Tempest 2000 (World).j64");

            cartDescriptor.LibretroCore.Should().Be("virtualjaguar");
            cartDescriptor.EmulatorJsCore.Should().Be("jaguar");
            cartDescriptor.SupportsWebAssembly.Should().BeTrue();
            cartDescriptor.WebAssemblyUnsupportedReason.Should().BeEmpty();
        }

        [Test]
        public void should_resolve_jaguar_cd_bios_and_keep_cue_bin_zip_intact()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_jaguarcd_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                var biosPath = Path.Combine(biosDir, "jagcd.rom");
                File.WriteAllBytes(biosPath, new byte[] { 1, 2, 3, 4 });

                var zipPath = Path.Combine(tempDir, "Battlemorph (World).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var cueEntry = archive.CreateEntry("Battlemorph (World).cue");
                    using (var s = cueEntry.Open())
                    {
                        var bytes = Encoding.ASCII.GetBytes("FILE \"Battlemorph (World) (Track 01).bin\" BINARY\n");
                        s.Write(bytes, 0, bytes.Length);
                    }

                    var binEntry = archive.CreateEntry("Battlemorph (World) (Track 01).bin");
                    using (var s = binEntry.Open())
                    {
                        s.Write(new byte[] { 0xAA, 0xBB }, 0, 2);
                    }
                }

                var descriptor = _subject.BuildLaunchDescriptor(56, "jaguarcd", zipPath);
                descriptor.EmulatorJsCore.Should().Be("jaguarcd");
                descriptor.SupportsWebAssembly.Should().BeFalse();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/56/bios/jagcd.rom");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/56/rom/Battlemorph%20%28World%29.zip");

                using var biosStream = _subject.OpenBiosStream(56, out var biosFileName, "jaguarcd", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("jagcd.rom");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_expose_jaguar_cd_and_jaguar_separately_in_platform_core_options()
        {
            var coreOptions = _subject.GetPlatformCoreOptions();

            coreOptions.Should().Contain(o =>
                o.PlatformKey == "Atari Jaguar CD" &&
                o.DefaultCore == "virtualjaguar" &&
                !o.SupportsWebAssembly &&
                o.EmulatorJsCoreName == "jaguarcd");

            coreOptions.Should().Contain(o =>
                o.PlatformKey == "Atari Jaguar" &&
                o.DefaultCore == "virtualjaguar" &&
                o.SupportsWebAssembly &&
                o.EmulatorJsCoreName == "jaguar");
        }

        [TestCase("neogeocd")]
        [TestCase("Neo Geo CD")]
        [TestCase("SNK - Neo Geo CD")]
        public void should_mark_neo_geo_cd_as_desktop_only_and_preserve_arcade_neo_geo_wasm(string platform)
        {
            var cdDescriptor = _subject.BuildLaunchDescriptor(130, platform, $"/roms/{platform}/Metal Slug (Japan).chd");

            cdDescriptor.LibretroCore.Should().Be("neocd");
            cdDescriptor.EmulatorJsCore.Should().Be("neocd");
            cdDescriptor.SupportsWebAssembly.Should().BeFalse();
            cdDescriptor.WebAssemblyUnsupportedReason.Should().Contain("neocd");

            var aesDescriptor = _subject.BuildLaunchDescriptor(131, "neogeo", "/roms/neogeo/mslug.zip");

            aesDescriptor.LibretroCore.Should().Be("fbneo");
            aesDescriptor.EmulatorJsCore.Should().Be("arcade");
            aesDescriptor.SupportsWebAssembly.Should().BeTrue();
            aesDescriptor.WebAssemblyUnsupportedReason.Should().BeEmpty();
        }

        [Test]
        public void should_resolve_neogeocd_satellaview_and_sufami_turbo_bios_files()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_addon_bios_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                File.WriteAllBytes(Path.Combine(biosDir, "neocd_z.rom"), new byte[] { 1, 2, 3, 4 });
                File.WriteAllBytes(Path.Combine(biosDir, "BS-X.bin"), new byte[] { 5, 6, 7, 8 });
                File.WriteAllBytes(Path.Combine(biosDir, "STBIOS.bin"), new byte[] { 9, 10, 11, 12 });

                var neocdRom = Path.Combine(tempDir, "Metal Slug (Japan).chd");
                var bsxRom = Path.Combine(tempDir, "BS Zelda no Densetsu (Japan).bs");
                var sufamiRom = Path.Combine(tempDir, "SD Ultra Battle (Japan).st");
                File.WriteAllBytes(neocdRom, new byte[] { 0x01 });
                File.WriteAllBytes(bsxRom, new byte[] { 0x02 });
                File.WriteAllBytes(sufamiRom, new byte[] { 0x03 });

                var neocdDescriptor = _subject.BuildLaunchDescriptor(140, "neogeocd", neocdRom);
                neocdDescriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/140/bios/neocd_z.rom");

                var bsxDescriptor = _subject.BuildLaunchDescriptor(141, "satellaview", bsxRom);
                bsxDescriptor.SupportsWebAssembly.Should().BeTrue();
                bsxDescriptor.EmulatorJsCore.Should().Be("snes");
                bsxDescriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/141/bios/BS-X.bin");

                var sufamiDescriptor = _subject.BuildLaunchDescriptor(142, "sufamiturbo", sufamiRom);
                sufamiDescriptor.SupportsWebAssembly.Should().BeTrue();
                sufamiDescriptor.EmulatorJsCore.Should().Be("snes");
                sufamiDescriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/142/bios/STBIOS.bin");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestCase("psp", "ppsspp")]
        [TestCase("PlayStation Portable", "ppsspp")]
        [TestCase("psppsn", "ppsspp")]
        public void should_mark_psp_as_desktop_only_due_to_shared_array_buffer_threads(string platform, string expectedCore)
        {
            var descriptor = _subject.BuildLaunchDescriptor(150, platform, $"/roms/{platform}/Crisis Core.cso");

            descriptor.LibretroCore.Should().Be(expectedCore);
            descriptor.SupportsWebAssembly.Should().BeFalse();
            descriptor.WebAssemblyUnsupportedReason.Should().Contain("SharedArrayBuffer");
        }

        [TestCase("PlayStation 3", "rpcs3", "/roms/ps3/Demon's Souls.iso")]
        [TestCase("Sony PlayStation 3 PSN", "rpcs3", "/roms/ps3psn/Journey.pkg")]
        [TestCase("PlayStation Vita", "vita3k", "/roms/psvita/Persona 4 Golden.vpk")]
        [TestCase("Wii U", "cemu", "/roms/wiiu/Mario Kart 8.wua")]
        [TestCase("Nintendo 3DS", "azahar", "/roms/3ds/Ocarina of Time 3D.3ds")]
        [TestCase("gp32", "mame", "/roms/gp32/Astonishia Story R.smc")]
        [TestCase("pico", "picodrive", "/roms/pico/Sonic the Hedgehog's Gameworld.md")]
        public void should_not_hijack_desktop_only_platforms_via_substring_or_extension(string platform, string expectedCore, string romPath)
        {
            var descriptor = _subject.BuildLaunchDescriptor(160, platform, romPath);

            descriptor.LibretroCore.Should().Be(expectedCore);
            descriptor.SupportsWebAssembly.Should().BeFalse();
        }

        [TestCase("Nintendo - Game Boy Advance (No-Intro)", "gba")]
        [TestCase("SNK - Neo Geo Pocket Color (No-Intro)", "ngp")]
        [TestCase("Sony - PlayStation 2 (Redump)", "ps2")]
        public void should_prefer_longest_matching_platform_alias_over_shorter_prefix(string platform, string expectedEjsCore)
        {
            var descriptor = _subject.BuildLaunchDescriptor(170, platform, "/roms/test/Game.bin");

            descriptor.EmulatorJsCore.Should().Be(expectedEjsCore);
        }

        [Test]
        public void should_not_infer_unrelated_wasm_platform_from_extension_when_platform_is_known()
        {
            var loopyDescriptor = _subject.BuildLaunchDescriptor(180, "loopy", "/roms/loopy/Anime Land (Japan).bin");
            var ereaderDescriptor = _subject.BuildLaunchDescriptor(181, "Nintendo e-Reader", "/roms/ereader/Mario Party-e.gba");

            loopyDescriptor.PlatformFolder.Should().Be("loopy");
            loopyDescriptor.SupportsWebAssembly.Should().BeFalse();

            ereaderDescriptor.PlatformFolder.Should().Be("Nintendo e-Reader");
            ereaderDescriptor.SupportsWebAssembly.Should().BeFalse();
        }

        [Test]
        public void should_resolve_pcfx_bios_and_keep_cue_bin_zip_intact()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_pcfx_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                File.WriteAllBytes(Path.Combine(biosDir, "pcfx.rom"), new byte[] { 1, 2, 3, 4 });

                var zipPath = Path.Combine(tempDir, "Team Innocent (Japan).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var cueEntry = archive.CreateEntry("Team Innocent (Japan).cue");
                    using (var s = cueEntry.Open())
                    {
                        var bytes = Encoding.ASCII.GetBytes("FILE \"Team Innocent (Japan) (Track 01).bin\" BINARY\n");
                        s.Write(bytes, 0, bytes.Length);
                    }

                    var binEntry = archive.CreateEntry("Team Innocent (Japan) (Track 01).bin");
                    using (var s = binEntry.Open())
                    {
                        s.Write(new byte[] { 0xAA, 0xBB }, 0, 2);
                    }
                }

                var descriptor = _subject.BuildLaunchDescriptor(190, "pcfx", zipPath);
                descriptor.LibretroCore.Should().Be("mednafen_pcfx");
                descriptor.EmulatorJsCore.Should().Be("pcfx");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/190/bios/pcfx.rom");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/190/rom/Team%20Innocent%20%28Japan%29.zip");

                using var biosStream = _subject.OpenBiosStream(190, out var biosFileName, "pcfx", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("pcfx.rom");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_resolve_cdi_bios_and_map_to_same_cdi_core()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_cdi_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                File.WriteAllBytes(Path.Combine(biosDir, "cdimono1.zip"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

                var romPath = Path.Combine(tempDir, "Hotel Mario (USA).chd");
                File.WriteAllBytes(romPath, new byte[] { 0x01, 0x02 });

                var descriptor = _subject.BuildLaunchDescriptor(191, "cdi", romPath);
                descriptor.LibretroCore.Should().Be("same_cdi");
                descriptor.EmulatorJsCore.Should().Be("same_cdi");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/191/bios/cdimono1.zip");

                using var biosStream = _subject.OpenBiosStream(191, out var biosFileName, "cdi", romPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("cdimono1.zip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_bundle_amiga_cd32_kickstart_pair_and_keep_cue_bin_zip_intact()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_cd32_test_" + Guid.NewGuid().ToString("N"));
            var biosDir = Path.Combine(tempDir, "BIOS");
            Directory.CreateDirectory(biosDir);

            try
            {
                var mainKickBytes = new byte[] { 0x11, 0x22, 0x33, 0x44 };
                var extKickBytes = new byte[] { 0x55, 0x66, 0x77, 0x88 };
                File.WriteAllBytes(Path.Combine(biosDir, "kick40060.CD32"), mainKickBytes);
                File.WriteAllBytes(Path.Combine(biosDir, "kick40060.CD32.ext"), extKickBytes);

                var zipPath = Path.Combine(tempDir, "Banshee (Europe).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var cueEntry = archive.CreateEntry("Banshee (Europe).cue");
                    using (var s = cueEntry.Open())
                    {
                        var bytes = Encoding.ASCII.GetBytes("FILE \"Banshee (Europe) (Track 01).bin\" BINARY\n");
                        s.Write(bytes, 0, bytes.Length);
                    }

                    var binEntry = archive.CreateEntry("Banshee (Europe) (Track 01).bin");
                    using (var s = binEntry.Open())
                    {
                        s.Write(new byte[] { 0xAA, 0xBB }, 0, 2);
                    }
                }

                var descriptor = _subject.BuildLaunchDescriptor(192, "cd32", zipPath);
                descriptor.LibretroCore.Should().Be("puae");
                descriptor.EmulatorJsCore.Should().Be("amiga");
                descriptor.SupportsWebAssembly.Should().BeTrue();
                descriptor.BiosStreamUrl.Should().StartWith("/api/v5/gamelauncher/192/bios/amiga_cd32_bios.zip");
                descriptor.RomStreamUrl.Should().StartWith("/api/v5/gamelauncher/192/rom/Banshee%20%28Europe%29.zip");

                using var biosStream = _subject.OpenBiosStream(192, out var biosFileName, "cd32", zipPath);
                biosStream.Should().NotBeNull();
                biosFileName.Should().Be("amiga_cd32_bios.zip");

                using var zip = new ZipArchive(biosStream, ZipArchiveMode.Read);
                zip.Entries.Should().Contain(e => e.FullName == "kick40060.CD32");
                zip.Entries.Should().Contain(e => e.FullName == "kick40060.CD32.ext");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestCase(@"C:\RetroArch\retroarch.exe", true)]
        [TestCase("/usr/bin/retroarch", true)]
        [TestCase("/Applications/RetroArch.app/Contents/MacOS/RetroArch", true)]
        [TestCase(@"C:\Windows\System32\cmd.exe", false)]
        [TestCase("/bin/sh", false)]
        [TestCase("", false)]
        public void should_only_allow_retroarch_executables_for_desktop_launch(string executablePath, bool expected)
        {
            GameLauncherService.IsAllowedRetroArchExecutable(executablePath).Should().Be(expected);
        }

        [Test]
        public void should_block_arbitrary_executable_and_rom_path_outside_configured_library()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_launcher_sec_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var fakeExe = Path.Combine(tempDir, "not_retroarch.exe");
                File.WriteAllText(fakeExe, "stub");

                var libDir = Path.Combine(tempDir, "SNES");
                Directory.CreateDirectory(libDir);
                var outsideRom = Path.Combine(tempDir, "outside.sfc");
                File.WriteAllBytes(outsideRom, new byte[] { 0x01, 0x02 });

                var seriesService = new Mock<ISeriesService>();
                seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
                {
                    new Series { Id = 1, Title = "SNES", TitleSlug = "snes", Path = libDir }
                });

                var launcher = new GameLauncherService(seriesService: seriesService.Object);
                launcher.SaveConfig(new LibretroLauncherConfig { RetroArchExecutablePath = fakeExe });

                var outsideDescriptor = launcher.BuildLaunchDescriptor(10, "snes", outsideRom);
                outsideDescriptor.HasRomFile.Should().BeFalse();

                var blockedExeResult = launcher.LaunchDesktopGame(10, "snes", outsideRom);
                blockedExeResult.Success.Should().BeFalse();
                blockedExeResult.Message.Should().Contain("not a recognized RetroArch binary");

                var retroarchExe = Path.Combine(tempDir, "retroarch.exe");
                File.WriteAllText(retroarchExe, "stub");
                launcher.SaveConfig(new LibretroLauncherConfig { RetroArchExecutablePath = retroarchExe });

                var blockedRomResult = launcher.LaunchDesktopGame(10, "snes", outsideRom);
                blockedRomResult.Success.Should().BeFalse();
                blockedRomResult.Message.Should().Contain("outside configured library folders");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
