using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Games.VirtualLibrary;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Games.VirtualLibrary
{
    [TestFixture]
    public class VirtualLibraryServiceFixture
    {
        private string _tempRoot;
        private string _appDataDir;
        private string _libraryDir;
        private string _virtualDir;
        private Mock<IAppFolderInfo> _appFolderInfo;
        private Mock<ISeriesService> _seriesService;
        private VirtualLibraryService _subject;

        [SetUp]
        public void Setup()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "Controllarr_VirtualLib_" + Guid.NewGuid().ToString("N"));
            _appDataDir = Path.Combine(_tempRoot, "appdata");
            _libraryDir = Path.Combine(_tempRoot, "library");
            _virtualDir = Path.Combine(_tempRoot, "virtual");

            Directory.CreateDirectory(_appDataDir);
            Directory.CreateDirectory(_libraryDir);
            Directory.CreateDirectory(_virtualDir);

            _appFolderInfo = new Mock<IAppFolderInfo>();
            _appFolderInfo.Setup(x => x.AppDataFolder).Returns(_appDataDir);

            _seriesService = new Mock<ISeriesService>();
            _subject = new VirtualLibraryService(
                _appFolderInfo.Object,
                _seriesService.Object,
                LogManager.GetCurrentClassLogger());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                DeleteDirectorySafe(_tempRoot);
            }
        }

        [Test]
        public void should_filter_unsupported_platforms_and_map_multisystem_cores_for_mister()
        {
            var nesDir = CreatePlatformDir("Nintendo - NES");
            var fdsDir = CreatePlatformDir("Nintendo - Famicom Disk System");
            var a2600Dir = CreatePlatformDir("Atari 2600");
            var a7800Dir = CreatePlatformDir("Atari 7800");
            var ds3Dir = CreatePlatformDir("Nintendo 3DS");
            var ps2Dir = CreatePlatformDir("Sony PlayStation 2");

            _seriesService.Setup(x => x.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Title = "Nintendo Entertainment System", TitleSlug = "nes", Path = nesDir },
                new Series { Title = "Famicom Disk System", TitleSlug = "fds", Path = fdsDir },
                new Series { Title = "Atari 2600", TitleSlug = "atari2600", Path = a2600Dir },
                new Series { Title = "Atari 7800", TitleSlug = "atari7800", Path = a7800Dir },
                new Series { Title = "Nintendo 3DS", TitleSlug = "3ds", Path = ds3Dir },
                new Series { Title = "PlayStation 2", TitleSlug = "ps2", Path = ps2Dir }
            });

            var profile = _subject.Save(new VirtualLibraryProfile
            {
                Name = "MiSTer",
                TargetPath = _virtualDir,
                HardwarePreset = "mister"
            });

            var synced = _subject.SyncVirtualLibrary(profile.Id);

            synced.LinkedConsoleCount.Should().Be(4);
            Directory.Exists(Path.Combine(_virtualDir, "games", "NES", "NES")).Should().BeTrue();
            Directory.Exists(Path.Combine(_virtualDir, "games", "NES", "FDS")).Should().BeTrue();
            Directory.Exists(Path.Combine(_virtualDir, "games", "Atari7800", "Atari2600")).Should().BeTrue();
            Directory.Exists(Path.Combine(_virtualDir, "games", "Atari7800", "Atari7800")).Should().BeTrue();

            Directory.Exists(Path.Combine(_virtualDir, "games", "3DS")).Should().BeFalse();
            Directory.Exists(Path.Combine(_virtualDir, "games", "PS2")).Should().BeFalse();
        }

        [Test]
        public void should_prune_stale_symlinks_and_migrate_1to1_core_symlink_to_subfolders()
        {
            var nesDir = CreatePlatformDir("NES");
            var fdsDir = CreatePlatformDir("FDS");
            var ds3Dir = CreatePlatformDir("3DS");

            var gamesDir = Path.Combine(_virtualDir, "games");
            Directory.CreateDirectory(gamesDir);

            // Simulate legacy 1:1 MiSTer links for NES and unsupported 3DS
            var legacyNesLink = Path.Combine(gamesDir, "NES");
            var legacy3dsLink = Path.Combine(gamesDir, "3DS");
            CreateJunctionOrSymlinkForTest(legacyNesLink, nesDir);
            CreateJunctionOrSymlinkForTest(legacy3dsLink, ds3Dir);

            _seriesService.Setup(x => x.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Title = "Nintendo Entertainment System", TitleSlug = "nes", Path = nesDir },
                new Series { Title = "Famicom Disk System", TitleSlug = "fds", Path = fdsDir },
                new Series { Title = "Nintendo 3DS", TitleSlug = "3ds", Path = ds3Dir }
            });

            var profile = _subject.Save(new VirtualLibraryProfile
            {
                Name = "MiSTer",
                TargetPath = _virtualDir,
                HardwarePreset = "mister"
            });

            var synced = _subject.SyncVirtualLibrary(profile.Id);

            synced.LinkedConsoleCount.Should().Be(2);
            Directory.Exists(Path.Combine(gamesDir, "NES", "NES")).Should().BeTrue();
            Directory.Exists(Path.Combine(gamesDir, "NES", "FDS")).Should().BeTrue();
            Directory.Exists(legacy3dsLink).Should().BeFalse();
        }

        [TestCase("mister", "nes", true)]
        [TestCase("mister", "fds", true)]
        [TestCase("mister", "3ds", false)]
        [TestCase("mister", "ps2", false)]
        [TestCase("analogue_pocket", "gbc", true)]
        [TestCase("analogue_pocket", "ps1", false)]
        [TestCase("onionos", "ps1", true)]
        [TestCase("onionos", "3ds", false)]
        [TestCase("batocera", "3ds", true)]
        [TestCase("retroarch", "ps2", true)]
        public void should_check_preset_platform_support(string preset, string slug, bool expected)
        {
            VirtualLibraryService.IsPlatformSupportedByPreset(preset, slug).Should().Be(expected);
        }

        [TestCase("retroarch", "ps1", "Sony - PlayStation")]
        [TestCase("retroarch", "segacd", "Sega - Mega-CD - Sega CD")]
        [TestCase("retroarch", "fds", "Nintendo - Family Computer Disk System")]
        [TestCase("retroarch", "sms", "Sega - Master System - Mark III")]
        [TestCase("retroarch", "tg16", "NEC - PC Engine - TurboGrafx 16")]
        [TestCase("retroarch", "neogeo", "SNK - Neo Geo")]
        [TestCase("batocera", "segacd", "segacd")]
        [TestCase("batocera", "ps1", "psx")]
        [TestCase("batocera", "sms", "mastersystem")]
        [TestCase("batocera", "tg16", "pcengine")]
        [TestCase("onionos", "fds", "FDS")]
        [TestCase("onionos", "sms", "MS")]
        [TestCase("onionos", "tg16", "PCE")]
        [TestCase("everdrive", "segacd", "MegaCD")]
        [TestCase("everdrive", "sms", "SMS")]
        [TestCase("everdrive", "tg16", "TGFX16")]
        public void should_map_short_canonical_slugs_for_hardware_presets(string preset, string slug, string expectedRelativeSegment)
        {
            var platformDir = CreatePlatformDir("Platform_" + slug);
            _seriesService.Setup(x => x.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Title = slug, TitleSlug = slug, Path = platformDir }
            });

            var targetDir = Path.Combine(_virtualDir, preset + "_" + slug);
            Directory.CreateDirectory(targetDir);

            var profile = _subject.Save(new VirtualLibraryProfile
            {
                Name = "Test_" + preset + "_" + slug,
                TargetPath = targetDir,
                HardwarePreset = preset
            });

            var synced = _subject.SyncVirtualLibrary(profile.Id);

            synced.LinkedConsoleCount.Should().Be(1);
            var expectedPath = preset switch
            {
                "onionos" => Path.Combine(targetDir, "Roms", expectedRelativeSegment),
                "batocera" => Path.Combine(targetDir, "roms", expectedRelativeSegment),
                _ => Path.Combine(targetDir, expectedRelativeSegment)
            };

            Directory.Exists(expectedPath).Should().BeTrue($"expected virtual library link at '{expectedPath}'");
        }

        private string CreatePlatformDir(string name)
        {
            var dir = Path.Combine(_libraryDir, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CreateJunctionOrSymlinkForTest(string linkPath, string targetPath)
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
            catch
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(3000);
            }
        }

        private static void DeleteDirectorySafe(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }

            foreach (var sub in Directory.GetDirectories(dir))
            {
                var info = new DirectoryInfo(sub);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    Directory.Delete(sub);
                }
                else
                {
                    DeleteDirectorySafe(sub);
                }
            }

            Directory.Delete(dir, true);
        }
    }
}
