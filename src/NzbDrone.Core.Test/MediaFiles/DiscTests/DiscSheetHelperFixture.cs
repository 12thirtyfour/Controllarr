using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Disc;

namespace NzbDrone.Core.Test.MediaFiles.DiscTests
{
    [TestFixture]
    public class DiscSheetHelperFixture
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dsh_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void should_resolve_mds_and_mdf_tracks_and_effective_size()
        {
            var mdsPath = Path.Combine(_tempDir, "Game (USA).mds");
            var mdfPath = Path.Combine(_tempDir, "Game (USA).mdf");
            File.WriteAllBytes(mdsPath, new byte[256]);
            File.WriteAllBytes(mdfPath, new byte[4096]);

            var tracks = DiscSheetHelper.ResolveTrackFiles(mdsPath);
            tracks.Should().ContainSingle().Which.Should().Be(Path.GetFullPath(mdfPath));
            DiscSheetHelper.GetEffectiveFileSize(mdsPath).Should().Be(256 + 4096);
        }

        [Test]
        public void should_find_companion_tracks_for_cue_bin_and_prefer_cue_over_secondary_ccd()
        {
            var cuePath = Path.Combine(_tempDir, "Apprentice (USA).cue");
            var ccdPath = Path.Combine(_tempDir, "Apprentice (USA).ccd");
            var imgPath = Path.Combine(_tempDir, "Apprentice (USA).img");
            var subPath = Path.Combine(_tempDir, "Apprentice (USA).sub");

            File.WriteAllText(cuePath, "FILE \"Apprentice (USA).img\" BINARY\r\n  TRACK 01 MODE1/2352\r\n");
            File.WriteAllText(ccdPath, "[CloneCD]\r\n");
            File.WriteAllBytes(imgPath, new byte[2048]);
            File.WriteAllBytes(subPath, new byte[512]);

            var companions = DiscSheetHelper.FindCompanionTrackFiles(new[] { cuePath, ccdPath, imgPath, subPath });

            companions.Should().Contain(Path.GetFullPath(imgPath));
            companions.Should().Contain(Path.GetFullPath(subPath));
            companions.Should().Contain(ccdPath);
            companions.Should().NotContain(cuePath);
        }

        [Test]
        public void should_ignore_binary_file_misnamed_as_cue_when_parsing_track_references()
        {
            var misnamedCue = Path.Combine(_tempDir, "MisnamedTrack (USA).cue");
            var headerBytes = new byte[] { 0x00, 0xFF, 0x00, 0x01 };
            var fakeCueText = Encoding.ASCII.GetBytes("\r\nFILE \"fake.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n");
            File.WriteAllBytes(misnamedCue, headerBytes.Concat(fakeCueText).ToArray());
            File.WriteAllBytes(Path.Combine(_tempDir, "fake.bin"), new byte[1024]);

            DiscSheetHelper.ParseTrackReferences(misnamedCue).Should().BeEmpty();
            DiscSheetHelper.ResolveTrackFilesPrioritizingData(misnamedCue).Should().BeEmpty();
        }
    }
}
