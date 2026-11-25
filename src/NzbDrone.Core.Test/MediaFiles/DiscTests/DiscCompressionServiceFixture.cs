using System;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.DiscTests
{
    [TestFixture]
    public class DiscCompressionServiceFixture : CoreTest<DiscCompressionService>
    {
        [Test]
        public void Should_build_chdman_createcd_command_for_ps1()
        {
            var source = Path.Combine("Library", "Final Fantasy VII (USA).cue");
            var (exe, args, format, output) = Subject.BuildCompressionCommand(source, "ps1");

            exe.Should().Be("chdman");
            format.Should().Be(DiscCompressionFormat.ChdCd);
            output.Should().EndWith(".chd");
            args.Should().Contain("createcd");
        }

        [Test]
        public void Should_build_chdman_createdvd_command_for_ps2()
        {
            var source = Path.Combine("Library", "Shadow of the Colossus (USA).iso");
            var (exe, args, format, output) = Subject.BuildCompressionCommand(source, "ps2");

            exe.Should().Be("chdman");
            format.Should().Be(DiscCompressionFormat.ChdDvd);
            output.Should().EndWith(".chd");
            args.Should().Contain("createdvd");
        }

        [Test]
        public void Should_build_dolphin_rvz_command_for_gamecube_and_wii()
        {
            var source = Path.Combine("Library", "Super Smash Bros. Melee (USA).iso");
            var (exe, args, format, output) = Subject.BuildCompressionCommand(source, "gamecube");

            exe.Should().Be("dolphin-tool");
            format.Should().Be(DiscCompressionFormat.Rvz);
            output.Should().EndWith(".rvz");
            args.Should().Contain("-f rvz");
        }

        [Test]
        public void Should_build_chdman_extractcd_command_for_flashcarts_that_do_not_support_chd()
        {
            var source = Path.Combine("Library", "Sega CD", "Sonic CD (USA).chd");
            var targetDir = Path.Combine("SDCard", "Sega CD", "N-S");

            var (exe, args, primaryOutput) = Subject.BuildDecompressionCommand(source, targetDir, "Sonic CD (USA)");

            exe.Should().Be("chdman");
            args.Should().Contain("extractcd");
            args.Should().Contain("-ob");
            primaryOutput.Should().Be(Path.Combine(targetDir, "Sonic CD (USA).cue"));
        }

        [Test]
        public void Should_compress_and_verify_chd_disc_via_process_provider()
        {
            Mocker.GetMock<IProcessProvider>()
                  .Setup(p => p.StartAndCapture(It.IsAny<string>(), It.IsAny<string>(), null))
                  .Returns(new ProcessOutput { ExitCode = 0 });

            var success = Subject.CompressDisc("Metal Gear Solid (USA).cue", "ps1", out var outputPath);

            success.Should().BeTrue();
            outputPath.Should().Be("Metal Gear Solid (USA).chd");
        }

        [Test]
        public void Should_generate_m3u_playlist_with_disc_file_names()
        {
            var tempM3u = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".m3u");

            try
            {
                Subject.GenerateM3uPlaylist(
                    new[]
                    {
                        Path.Combine("PS1", "Final Fantasy VII (USA) (Disc 1).chd"),
                        Path.Combine("PS1", "Final Fantasy VII (USA) (Disc 2).chd")
                    },
                    tempM3u);

                var lines = File.ReadAllLines(tempM3u);
                lines.Should().Equal("Final Fantasy VII (USA) (Disc 1).chd", "Final Fantasy VII (USA) (Disc 2).chd");
            }
            finally
            {
                if (File.Exists(tempM3u))
                {
                    File.Delete(tempM3u);
                }
            }
        }

        [Test]
        public void Should_extract_chd_tracks_using_splitbin_flag_when_supported()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr-chd-split-" + Guid.NewGuid().ToString("N"));
            var cuePath = Path.Combine(tempDir, "disc.cue");

            try
            {
                Mocker.GetMock<IProcessProvider>()
                    .Setup(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && a.Contains("-sb")), null))
                    .Returns(() =>
                    {
                        Directory.CreateDirectory(tempDir);
                        File.WriteAllText(cuePath, "FILE \"disc (Track 1).bin\" BINARY\n  TRACK 01 AUDIO\n");
                        return new ProcessOutput { ExitCode = 0 };
                    });

                var result = Subject.ExtractChdTracksForVerification("Game (USA).chd", tempDir, out var resolvedCuePath);

                result.Should().BeTrue();
                resolvedCuePath.Should().Be(cuePath);
                Mocker.GetMock<IProcessProvider>()
                    .Verify(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && a.Contains("-sb")), null), Times.Once());
                Mocker.GetMock<IProcessProvider>()
                    .Verify(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && !a.Contains("-sb")), null), Times.Never());
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
        public void Should_fallback_to_non_splitbin_extractcd_when_splitbin_fails()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr-chd-fallback-" + Guid.NewGuid().ToString("N"));
            var cuePath = Path.Combine(tempDir, "disc.cue");

            try
            {
                Mocker.GetMock<IProcessProvider>()
                    .Setup(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && a.Contains("-sb")), null))
                    .Returns(new ProcessOutput { ExitCode = 1 });

                Mocker.GetMock<IProcessProvider>()
                    .Setup(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && !a.Contains("-sb")), null))
                    .Returns(() =>
                    {
                        Directory.CreateDirectory(tempDir);
                        File.WriteAllText(cuePath, "FILE \"disc.bin\" BINARY\n  TRACK 01 AUDIO\n");
                        return new ProcessOutput { ExitCode = 0 };
                    });

                var result = Subject.ExtractChdTracksForVerification("Game (USA).chd", tempDir, out var resolvedCuePath);

                result.Should().BeTrue();
                resolvedCuePath.Should().Be(cuePath);
                Mocker.GetMock<IProcessProvider>()
                    .Verify(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && a.Contains("-sb")), null), Times.Once());
                Mocker.GetMock<IProcessProvider>()
                    .Verify(p => p.StartAndCapture("chdman", It.Is<string>(a => a.Contains("extractcd") && !a.Contains("-sb")), null), Times.Once());
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
        public void Should_prioritize_data_tracks_over_audio_tracks_in_cue_sheet()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr-cue-prio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var track1 = Path.Combine(tempDir, "Game (Track 1).bin");
                var track2 = Path.Combine(tempDir, "Game (Track 2).bin");
                var track3 = Path.Combine(tempDir, "Game (Track 3).bin");
                var cue = Path.Combine(tempDir, "Game.cue");

                File.WriteAllBytes(track1, new byte[] { 1, 2, 3 });
                File.WriteAllBytes(track2, new byte[] { 4, 5, 6 });
                File.WriteAllBytes(track3, new byte[] { 7, 8, 9 });
                File.WriteAllText(cue, string.Join(
                    "\n",
                    "FILE \"Game (Track 1).bin\" BINARY",
                    "  TRACK 01 AUDIO",
                    "    INDEX 01 00:00:00",
                    "FILE \"Game (Track 2).bin\" BINARY",
                    "  TRACK 02 MODE1/2352",
                    "    INDEX 00 00:00:00",
                    "    INDEX 01 00:02:00",
                    "FILE \"Game (Track 3).bin\" BINARY",
                    "  TRACK 03 AUDIO",
                    "    INDEX 01 00:00:00"));

                var orderedTracks = DiscSheetHelper.ResolveTrackFilesPrioritizingData(cue);

                orderedTracks.Should().Equal(Path.GetFullPath(track2), Path.GetFullPath(track1), Path.GetFullPath(track3));
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
