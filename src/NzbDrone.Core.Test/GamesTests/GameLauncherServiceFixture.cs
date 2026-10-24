using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games.Launcher;

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
    }
}
