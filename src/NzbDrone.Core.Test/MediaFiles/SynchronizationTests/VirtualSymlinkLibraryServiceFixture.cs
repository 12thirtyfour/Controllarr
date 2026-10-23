using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Synchronization;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.SynchronizationTests
{
    [TestFixture]
    public class VirtualSymlinkLibraryServiceFixture : CoreTest<VirtualSymlinkLibraryService>
    {
        [Test]
        public void Should_resolve_mister_retroarch_and_batocera_symlink_paths()
        {
            var root = Path.Combine("mnt", "nas");

            Subject.BuildVirtualSymlinkPath(root, SymlinkTargetProfile.MiSTer, "snes", "Chrono Trigger (USA).sfc")
                   .Should().Be(Path.Combine(root, "games", "SNES", "Chrono Trigger (USA).sfc"));

            Subject.BuildVirtualSymlinkPath(root, SymlinkTargetProfile.RetroArch, "genesis", "Sonic the Hedgehog (USA).md")
                   .Should().Be(Path.Combine(root, "roms", "Sega - Mega Drive - Genesis", "Sonic the Hedgehog (USA).md"));

            Subject.BuildVirtualSymlinkPath(root, SymlinkTargetProfile.Batocera, "ps1", "Castlevania - Symphony of the Night (USA).chd")
                   .Should().Be(Path.Combine(root, "roms", "psx", "Castlevania - Symphony of the Night (USA).chd"));
        }

        [Test]
        public void Should_generate_virtual_library_plan_without_touching_disk_when_dry_run()
        {
            var root = Path.Combine("mnt", "mister");
            var entries = new List<(string PlatformSlug, string SourceRomPath)>
            {
                ("snes", Path.Combine("Library", "Nintendo", "Super Nintendo", "Super Mario World (USA).sfc")),
                ("segacd", Path.Combine("Library", "Sega", "Sega CD", "Sonic CD (USA).chd"))
            };

            var links = Subject.SyncVirtualLibrary(root, SymlinkTargetProfile.MiSTer, entries, createFilesystemLinks: false);

            links.Should().HaveCount(2);
            links[0].Should().Be(Path.Combine(root, "games", "SNES", "Super Mario World (USA).sfc"));
            links[1].Should().Be(Path.Combine(root, "games", "MegaCD", "Sonic CD (USA).chd"));
        }
    }
}
