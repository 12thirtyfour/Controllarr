using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Games.VirtualLibrary;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Parser;
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

        [TestCase("mister")]
        [TestCase("retroarch")]
        [TestCase("analogue_pocket")]
        [TestCase("onionos")]
        [TestCase("batocera")]
        [TestCase("everdrive")]
        public void should_have_zero_path_or_ancestor_collisions_across_all_91_platforms(string preset)
        {
            var allPlatforms = GameCatalogProxy.GetBuiltInPlatforms();
            allPlatforms.Count.Should().Be(91);

            var resolvedPaths = new List<(string Slug, string RelativePath)>();
            foreach (var platform in allPlatforms)
            {
                var canonicalSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(platform.Slug, platform.Name);
                canonicalSlug.Should().NotBeNullOrWhiteSpace($"platform '{platform.Slug}' should normalize cleanly");

                if (!VirtualLibraryService.IsPlatformSupportedByPreset(preset, canonicalSlug))
                {
                    continue;
                }

                var relPath = VirtualLibraryService.ResolvePresetRelativePath(preset, canonicalSlug, platform.Slug)
                    .Replace('\\', '/')
                    .Trim('/');

                relPath.Should().NotBeNullOrWhiteSpace($"preset '{preset}' should map '{canonicalSlug}'");
                resolvedPaths.Add((canonicalSlug, relPath));
            }

            resolvedPaths.Select(p => p.RelativePath.ToLowerInvariant())
                .Should()
                .OnlyHaveUniqueItems($"preset '{preset}' should not collide two platforms onto the same folder");

            foreach (var a in resolvedPaths)
            {
                foreach (var b in resolvedPaths)
                {
                    if (string.Equals(a.Slug, b.Slug, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    b.RelativePath.StartsWith(a.RelativePath + "/", StringComparison.OrdinalIgnoreCase)
                        .Should()
                        .BeFalse($"preset '{preset}' maps '{a.Slug}' to '{a.RelativePath}', which is an ancestor of '{b.Slug}' ('{b.RelativePath}')");
                }
            }
        }

        [Test]
        public void should_map_newly_added_and_special_platforms_across_presets()
        {
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "switcheshop").Should().Be("Nintendo - Switch (Digital)");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "switch").Should().Be("Nintendo - Switch");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "atari5200").Should().Be("Atari - 5200");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "jaguarcd").Should().Be("Atari - Jaguar CD");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "pcfx").Should().Be("NEC - PC-FX");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "wiiu").Should().Be("Nintendo - Wii U");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "xbox360digital").Should().Be("Microsoft - Xbox 360 (Digital)");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "ps3psn").Should().Be("Sony - PlayStation 3 (PSN)");
            VirtualLibraryService.ResolvePresetRelativePath("retroarch", "ereader").Should().Be("Nintendo - e-Reader");

            VirtualLibraryService.ResolvePresetRelativePath("batocera", "64dd").Replace('\\', '/').Should().Be("roms/n64dd");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "vb").Replace('\\', '/').Should().Be("roms/virtualboy");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "pokemonmini").Replace('\\', '/').Should().Be("roms/pokemini");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "jaguar").Replace('\\', '/').Should().Be("roms/atarijaguar");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "jaguarcd").Replace('\\', '/').Should().Be("roms/atarijaguarcd");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "odyssey2").Replace('\\', '/').Should().Be("roms/o2em");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "videopacplus").Replace('\\', '/').Should().Be("roms/videopacplus");
            VirtualLibraryService.ResolvePresetRelativePath("batocera", "superacan").Replace('\\', '/').Should().Be("roms/supracan");

            VirtualLibraryService.IsPlatformSupportedByPreset("mister", "channelf").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("mister", "cd32").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("mister", "pokemonmini").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("mister", "gameandwatch").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("mister", "gx4000").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("analogue_pocket", "tgcd").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("analogue_pocket", "atari5200").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("onionos", "vb").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("onionos", "dsiware").Should().BeTrue();
            VirtualLibraryService.IsPlatformSupportedByPreset("everdrive", "gamecube").Should().BeTrue();
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
