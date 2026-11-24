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
        [TestCase("Game Boy Color", "gbc")]
        [TestCase("Game Boy Advance", "gba")]
        [TestCase("Virtual Boy", "vb")]
        [TestCase("Nintendo 64", "n64")]
        [TestCase("Nintendo DS", "nds")]
        [TestCase("Genesis", "segaMD")]
        [TestCase("Master System", "segaMS")]
        [TestCase("Game Gear", "segaGG")]
        [TestCase("segacd", "segaCD")]
        [TestCase("sega32x", "sega32x")]
        [TestCase("saturn", "segaSaturn")]
        [TestCase("psx", "psx")]
        [TestCase("psp", "psp")]
        [TestCase("tg16", "pce")]
        [TestCase("tgcd", "pce")]
        [TestCase("neogeo", "arcade")]
        [TestCase("ngp", "ngp")]
        [TestCase("ws", "ws")]
        [TestCase("atari2600", "atari2600")]
        [TestCase("atari5200", "atari5200")]
        [TestCase("atari7800", "atari7800")]
        [TestCase("lynx", "lynx")]
        [TestCase("jaguar", "jaguar")]
        [TestCase("coleco", "coleco")]
        [TestCase("3do", "3do")]
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
                descriptor.EmulatorJsCore.Should().Be("n64");
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
    }
}
