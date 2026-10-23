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
    }
}
