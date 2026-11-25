using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.Patching
{
    [TestFixture]
    public class RomPatchingServiceFixture
    {
        private RomPatchingService _subject;
        private Mock<ISeriesService> _seriesServiceMock;
        private Mock<IEpisodeService> _episodeServiceMock;
        private Mock<IMediaFileService> _mediaFileServiceMock;
        private Mock<IDatCacheService> _datCacheServiceMock;

        [SetUp]
        public void Setup()
        {
            _seriesServiceMock = new Mock<ISeriesService>();
            _episodeServiceMock = new Mock<IEpisodeService>();
            _mediaFileServiceMock = new Mock<IMediaFileService>();
            _datCacheServiceMock = new Mock<IDatCacheService>();

            _subject = new RomPatchingService(
                null,
                _seriesServiceMock.Object,
                _episodeServiceMock.Object,
                _mediaFileServiceMock.Object,
                _datCacheServiceMock.Object,
                null);
        }

        [Test]
        public void should_apply_ips_patch_with_standard_and_rle_records()
        {
            var source = Encoding.ASCII.GetBytes("HELLO_RETAIL_ROM_DATA");

            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("PATCH"));

            // Record 1: overwrite offset 6, length 6 with "HACKED"
            ms.Write(new byte[] { 0x00, 0x00, 0x06, 0x00, 0x06 });
            ms.Write(Encoding.ASCII.GetBytes("HACKED"));

            // Record 2: RLE at offset 21, length 3 of '!'
            ms.Write(new byte[] { 0x00, 0x00, 0x15, 0x00, 0x00, 0x00, 0x03, (byte)'!' });

            ms.Write(Encoding.ASCII.GetBytes("EOF"));
            var patchBytes = ms.ToArray();

            _subject.DetectFormat(patchBytes).Should().Be(RomPatchFormat.Ips);
            var patched = _subject.ApplyPatch(source, patchBytes);

            Encoding.ASCII.GetString(patched).Should().Be("HELLO_HACKED_ROM_DATA!!!");
            Encoding.ASCII.GetString(source).Should().Be("HELLO_RETAIL_ROM_DATA");
        }

        [Test]
        public void should_apply_bps_patch_and_verify_source_target_and_patch_crc32()
        {
            var source = Encoding.ASCII.GetBytes("SUPER_MARIO_BROS_RETAIL_10");
            var expectedTarget = Encoding.ASCII.GetBytes("SUPER_MARIO_BROS_3MIX_HACK");

            var bpsPatch = BuildBpsTargetReadPatch(source, expectedTarget);

            _subject.DetectFormat(bpsPatch).Should().Be(RomPatchFormat.Bps);
            _subject.TryReadBpsSourceCrc32(bpsPatch).Should().Be(RomPatchingService.ComputeCrc32Uint(source, 0, source.Length));
            _subject.TryReadBpsTargetCrc32(bpsPatch).Should().Be(RomPatchingService.ComputeCrc32Uint(expectedTarget, 0, expectedTarget.Length));

            var patched = _subject.ApplyPatch(source, bpsPatch);
            patched.Should().Equal(expectedTarget);

            // Corrupting source bytes must fail the BPS cryptographic source CRC32 check
            var wrongSource = Encoding.ASCII.GetBytes("WRONG_RETAIL_ROM_BYTES_000");
            Action act = () => _subject.ApplyPatch(wrongSource, bpsPatch);
            act.Should().Throw<InvalidDataException>().WithMessage("*source ROM CRC32 mismatch*");
        }

        [Test]
        public void should_apply_xdelta_vcdiff_patch()
        {
            var source = Encoding.ASCII.GetBytes("MOTHER_3_JAPAN_RETAIL");
            var expectedTarget = Encoding.ASCII.GetBytes("MOTHER_3_ENGLISH_V1_3");

            // Build a single-window RFC 3284 VCDIFF patch using ADD instruction (opcode 1 with size in inst stream)
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 0xD6, 0xC3, 0xC4, 0x00 }); // VCDIFF magic
            ms.WriteByte(0x00); // Hdr_Indicator = 0

            // Window 1: Win_Indicator = 0 (no source segment needed for pure ADD)
            ms.WriteByte(0x00);

            // Instructions: opcode 1 = ADD size 0 (followed by varint size = 21)
            var instructions = new byte[] { 0x01, (byte)expectedTarget.Length };

            using var deltaBody = new MemoryStream();
            deltaBody.WriteByte((byte)expectedTarget.Length); // Target window length
            deltaBody.WriteByte(0x00); // Delta_Indicator = 0
            deltaBody.WriteByte((byte)expectedTarget.Length); // Length of data for ADDs and RUNs
            deltaBody.WriteByte((byte)instructions.Length); // Length of instructions
            deltaBody.WriteByte(0x00); // Length of addresses
            deltaBody.Write(expectedTarget, 0, expectedTarget.Length);
            deltaBody.Write(instructions, 0, instructions.Length);

            var deltaBytes = deltaBody.ToArray();
            ms.WriteByte((byte)deltaBytes.Length); // Length of the delta encoding
            ms.Write(deltaBytes, 0, deltaBytes.Length);

            var vcdiffPatch = ms.ToArray();
            _subject.DetectFormat(vcdiffPatch).Should().Be(RomPatchFormat.Xdelta);

            var patched = _subject.ApplyPatch(source, vcdiffPatch);
            patched.Should().Equal(expectedTarget);
        }

        [Test]
        public void should_apply_patch_onto_library_retail_rom_and_output_to_rom_hacks_without_modifying_retail_file()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Controllarr_PatchTest_" + Guid.NewGuid().ToString("N"));
            var usaDir = Path.Combine(tempDir, "USA");
            Directory.CreateDirectory(usaDir);

            try
            {
                var retailPath = Path.Combine(usaDir, "Mother 3 (USA).gba");
                var retailBytes = Encoding.ASCII.GetBytes("CLEAN_RETAIL_1G1R_MOTHER3_ROM");
                File.WriteAllBytes(retailPath, retailBytes);

                var targetBytes = Encoding.ASCII.GetBytes("TRANSLATED_MOTHER3_ENG_V1_3!");
                var bpsPatch = BuildBpsTargetReadPatch(retailBytes, targetBytes);

                var series = new Series
                {
                    Id = 1,
                    TvdbId = 1011,
                    Title = "Game Boy Advance",
                    TitleSlug = "gba",
                    Path = tempDir
                };

                _seriesServiceMock.Setup(s => s.GetSeries(1)).Returns(series);
                _mediaFileServiceMock.Setup(m => m.GetFilesBySeries(1)).Returns(new List<EpisodeFile>
                {
                    new EpisodeFile
                    {
                        Id = 10,
                        SeriesId = 1,
                        SeasonNumber = 1,
                        RelativePath = Path.Combine("USA", "Mother 3 (USA).gba")
                    }
                });
                _episodeServiceMock.Setup(e => e.GetEpisodeBySeries(1)).Returns(new List<Episode>());
                _mediaFileServiceMock.Setup(m => m.GetFilesWithRelativePath(1, It.IsAny<string>())).Returns(new List<EpisodeFile>());
                _mediaFileServiceMock.Setup(m => m.Add(It.IsAny<EpisodeFile>())).Returns((EpisodeFile f) =>
                {
                    f.Id = 99;
                    return f;
                });

                var result = _subject.ApplyPatchToLibrary(new RomPatchRequestModel
                {
                    SeriesId = 1,
                    PatchFileName = "Mother 3 [T+Eng v1.3].bps",
                    PatchBase64Content = Convert.ToBase64String(bpsPatch)
                });

                result.Success.Should().BeTrue();
                result.OutputRomPath.Should().Contain("ROM Hacks");
                File.Exists(result.OutputRomPath).Should().BeTrue();
                File.ReadAllBytes(result.OutputRomPath).Should().Equal(targetBytes);

                // Clean retail 1G1R file must remain 100% unmodified
                File.ReadAllBytes(retailPath).Should().Equal(retailBytes);
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
        public void should_never_apply_ips_patch_to_unrelated_single_file_platform_like_3do_chd()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "Controllarr_PatchCrossPlatform_" + Guid.NewGuid().ToString("N"));
            var threeDoUsa = Path.Combine(tempRoot, "3do", "USA");
            var gbaUsa = Path.Combine(tempRoot, "gba", "USA");
            Directory.CreateDirectory(threeDoUsa);
            Directory.CreateDirectory(gbaUsa);

            try
            {
                var threeDoChdPath = Path.Combine(threeDoUsa, "Road Rash (USA).chd");
                File.WriteAllBytes(threeDoChdPath, Encoding.ASCII.GetBytes("FAKE_3DO_CHD_CONTAINER"));

                var fireRedPath = Path.Combine(gbaUsa, "Pokemon - FireRed Version (USA, Europe).gba");
                File.WriteAllBytes(fireRedPath, Encoding.ASCII.GetBytes("FIRE_RED_RETAIL_GBA_ROM"));

                var threeDoSeries = new Series { Id = 10, TvdbId = 1041, Title = "3DO Interactive Multiplayer", TitleSlug = "3do", Path = Path.Combine(tempRoot, "3do") };
                var gbaSeries = new Series { Id = 11, TvdbId = 1011, Title = "Nintendo Game Boy Advance", TitleSlug = "gba", Path = Path.Combine(tempRoot, "gba") };

                _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series> { threeDoSeries, gbaSeries });
                _mediaFileServiceMock.Setup(m => m.GetFilesBySeries(10)).Returns(new List<EpisodeFile>
                {
                    new EpisodeFile { Id = 100, SeriesId = 10, SeasonNumber = 1, RelativePath = Path.Combine("USA", "Road Rash (USA).chd") }
                });
                _mediaFileServiceMock.Setup(m => m.GetFilesBySeries(11)).Returns(new List<EpisodeFile>
                {
                    new EpisodeFile { Id = 101, SeriesId = 11, SeasonNumber = 1, RelativePath = Path.Combine("USA", "Pokemon - FireRed Version (USA, Europe).gba") }
                });

                var ipsPatch = BuildSimpleIpsPatch();

                var result = _subject.ApplyPatchToLibrary(new RomPatchRequestModel
                {
                    PatchFileName = "Pokemon Orange Islands Beta 5.7.ips",
                    PatchBase64Content = Convert.ToBase64String(ipsPatch)
                });

                result.Success.Should().BeFalse();
                result.Message.Should().Contain("select the target Platform and Retail Base ROM");
                Directory.Exists(Path.Combine(threeDoSeries.Path, "ROM Hacks")).Should().BeFalse();
                Directory.Exists(Path.Combine(gbaSeries.Path, "ROM Hacks")).Should().BeFalse();
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void should_reject_compressed_disc_containers_like_chd_for_ips_and_bps()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Controllarr_PatchChd_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var chdPath = Path.Combine(tempDir, "Road Rash (USA).chd");
                var chdBytes = Encoding.ASCII.GetBytes("FAKE_CHD_CONTAINER_DATA");
                File.WriteAllBytes(chdPath, chdBytes);

                var ipsResult = _subject.ApplyPatchToLibrary(new RomPatchRequestModel
                {
                    BaseRomPath = chdPath,
                    PatchFileName = "Pokemon Orange Islands Beta 5.7.ips",
                    PatchBase64Content = Convert.ToBase64String(BuildSimpleIpsPatch())
                });

                ipsResult.Success.Should().BeFalse();
                ipsResult.Message.Should().Contain("compressed disc container");

                var bpsResult = _subject.ApplyPatchToLibrary(new RomPatchRequestModel
                {
                    BaseRomPath = chdPath,
                    PatchFileName = "DiscPatch.bps",
                    PatchBase64Content = Convert.ToBase64String(BuildBpsTargetReadPatch(chdBytes, chdBytes))
                });

                bpsResult.Success.Should().BeFalse();
                bpsResult.Message.Should().Contain("compressed disc container");
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
        public void should_apply_ips_patch_to_explicitly_selected_zipped_gba_base_rom_with_accurate_metadata()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Controllarr_PatchZippedGba_" + Guid.NewGuid().ToString("N"));
            var usaDir = Path.Combine(tempDir, "USA");
            Directory.CreateDirectory(usaDir);

            try
            {
                var retailBytes = Encoding.ASCII.GetBytes("HELLO_RETAIL_ROM_DATA");
                var zipPath = Path.Combine(usaDir, "Pokemon - FireRed Version (USA, Europe).zip");
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("Pokemon - FireRed Version (USA, Europe).gba");
                    using var stream = entry.Open();
                    stream.Write(retailBytes, 0, retailBytes.Length);
                }

                var originalZipBytes = File.ReadAllBytes(zipPath);

                var gbaSeries = new Series
                {
                    Id = 11,
                    TvdbId = 1011,
                    Title = "Nintendo Game Boy Advance",
                    TitleSlug = "gba",
                    Path = tempDir
                };

                _seriesServiceMock.Setup(s => s.GetSeries(11)).Returns(gbaSeries);
                _mediaFileServiceMock.Setup(m => m.GetFilesBySeries(11)).Returns(new List<EpisodeFile>
                {
                    new EpisodeFile
                    {
                        Id = 202,
                        SeriesId = 11,
                        SeasonNumber = 1,
                        RelativePath = Path.Combine("USA", "Pokemon - FireRed Version (USA, Europe).zip")
                    }
                });
                _episodeServiceMock.Setup(e => e.GetEpisodeBySeries(11)).Returns(new List<Episode>());
                _mediaFileServiceMock.Setup(m => m.GetFilesWithRelativePath(11, It.IsAny<string>())).Returns(new List<EpisodeFile>());
                _mediaFileServiceMock.Setup(m => m.Add(It.IsAny<EpisodeFile>())).Returns((EpisodeFile f) =>
                {
                    f.Id = 303;
                    return f;
                });

                var result = _subject.ApplyPatchToLibrary(new RomPatchRequestModel
                {
                    SeriesId = 11,
                    BaseEpisodeFileId = 202,
                    PatchFileName = "Pokemon Orange Islands Beta 5.7.ips",
                    PatchBase64Content = Convert.ToBase64String(BuildSimpleIpsPatch())
                });

                result.Success.Should().BeTrue();
                result.Format.Should().Be("IPS");
                result.PlatformTitle.Should().Be("Nintendo Game Boy Advance");
                result.BaseRomName.Should().Be("Pokemon - FireRed Version (USA, Europe).zip");
                result.BaseRomCrc32.Should().Be(RomPatchingService.ComputeCrc32Uint(retailBytes, 0, retailBytes.Length).ToString("x8"));
                result.OutputRomPath.Should().EndWith(".gba");
                result.OutputRomPath.Should().Contain("ROM Hacks");
                result.VerifiedInCatalog.Should().BeFalse();
                result.MatchedCatalogName.Should().BeNull();
                Encoding.ASCII.GetString(File.ReadAllBytes(result.OutputRomPath)).Should().Be("HELLO_HACKED_ROM_DATA!!!");
                File.ReadAllBytes(zipPath).Should().Equal(originalZipBytes);
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
        public void should_inspect_ips_and_bps_patches_without_writing_files()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Controllarr_InspectPatch_" + Guid.NewGuid().ToString("N"));
            var usaDir = Path.Combine(tempDir, "USA");
            Directory.CreateDirectory(usaDir);

            try
            {
                var retailPath = Path.Combine(usaDir, "Mother 3 (USA).gba");
                var retailBytes = Encoding.ASCII.GetBytes("CLEAN_RETAIL_1G1R_MOTHER3_ROM");
                File.WriteAllBytes(retailPath, retailBytes);
                var expectedSrcCrc = RomPatchingService.ComputeCrc32Uint(retailBytes, 0, retailBytes.Length).ToString("x8");

                var targetBytes = Encoding.ASCII.GetBytes("TRANSLATED_MOTHER3_ENG_V1_3!");
                var bpsPatch = BuildBpsTargetReadPatch(retailBytes, targetBytes);

                var series = new Series
                {
                    Id = 5,
                    TvdbId = 1011,
                    Title = "Nintendo Game Boy Advance",
                    TitleSlug = "gba",
                    Path = tempDir
                };

                _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series> { series });
                _mediaFileServiceMock.Setup(m => m.GetFilesBySeries(5)).Returns(new List<EpisodeFile>
                {
                    new EpisodeFile
                    {
                        Id = 55,
                        SeriesId = 5,
                        SeasonNumber = 1,
                        RelativePath = Path.Combine("USA", "Mother 3 (USA).gba")
                    }
                });

                var bpsInspect = _subject.InspectPatch(new RomPatchRequestModel
                {
                    PatchFileName = "Mother 3 [T+Eng].bps",
                    PatchBase64Content = Convert.ToBase64String(bpsPatch)
                });

                bpsInspect.Success.Should().BeTrue();
                bpsInspect.Format.Should().Be("BPS");
                bpsInspect.HasEmbeddedSourceChecksum.Should().BeTrue();
                bpsInspect.ExpectedSourceCrc32.Should().Be(expectedSrcCrc);
                bpsInspect.RequiresManualBaseRom.Should().BeFalse();
                bpsInspect.AutoMatchedSeriesId.Should().Be(5);
                bpsInspect.AutoMatchedBaseEpisodeFileId.Should().Be(55);
                bpsInspect.AutoMatchedBaseRomName.Should().Be("Mother 3 (USA).gba");

                var ipsInspect = _subject.InspectPatch(new RomPatchRequestModel
                {
                    PatchFileName = "Pokemon Orange Islands Beta 5.7.ips",
                    PatchBase64Content = Convert.ToBase64String(BuildSimpleIpsPatch())
                });

                ipsInspect.Success.Should().BeTrue();
                ipsInspect.Format.Should().Be("IPS");
                ipsInspect.HasEmbeddedSourceChecksum.Should().BeFalse();
                ipsInspect.RequiresManualBaseRom.Should().BeTrue();
                ipsInspect.MaxPatchEndOffset.Should().Be(24);
                Directory.Exists(Path.Combine(tempDir, "ROM Hacks")).Should().BeFalse();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static byte[] BuildSimpleIpsPatch()
        {
            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("PATCH"));
            ms.Write(new byte[] { 0x00, 0x00, 0x06, 0x00, 0x06 });
            ms.Write(Encoding.ASCII.GetBytes("HACKED"));
            ms.Write(new byte[] { 0x00, 0x00, 0x15, 0x00, 0x00, 0x00, 0x03, (byte)'!' });
            ms.Write(Encoding.ASCII.GetBytes("EOF"));
            return ms.ToArray();
        }

        private static byte[] BuildBpsTargetReadPatch(byte[] source, byte[] target)
        {
            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("BPS1"));
            WriteBpsVarInt(ms, source.Length);
            WriteBpsVarInt(ms, target.Length);
            WriteBpsVarInt(ms, 0); // metadata length = 0

            // Action: TargetRead (mode 1) for target.Length bytes
            var action = ((long)(target.Length - 1) << 2) | 1L;
            WriteBpsVarInt(ms, action);
            ms.Write(target, 0, target.Length);

            var srcCrc = RomPatchingService.ComputeCrc32Uint(source, 0, source.Length);
            var tgtCrc = RomPatchingService.ComputeCrc32Uint(target, 0, target.Length);
            ms.Write(BitConverter.GetBytes(srcCrc), 0, 4);
            ms.Write(BitConverter.GetBytes(tgtCrc), 0, 4);

            var bodyPlusTwoCrcs = ms.ToArray();
            var patchCrc = RomPatchingService.ComputeCrc32Uint(bodyPlusTwoCrcs, 0, bodyPlusTwoCrcs.Length);
            ms.Write(BitConverter.GetBytes(patchCrc), 0, 4);

            return ms.ToArray();
        }

        private static void WriteBpsVarInt(Stream stream, long value)
        {
            while (true)
            {
                var x = value & 0x7F;
                value >>= 7;
                if (value == 0)
                {
                    stream.WriteByte((byte)(0x80 | x));
                    break;
                }

                stream.WriteByte((byte)x);
                value--;
            }
        }
    }
}
