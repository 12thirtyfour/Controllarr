using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.RomFileMovingServiceTests
{
    [TestFixture]
    public class DiscArchiveMoveFixture : CoreTest<RomFileMovingService>
    {
        private string _tempRoot;
        private string _downloadsDir;
        private string _libraryRoot;
        private Series _threeDoPlatform;

        [SetUp]
        public void Setup()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_disc_move_" + Guid.NewGuid().ToString("N"));
            _downloadsDir = Path.Combine(_tempRoot, "Downloads");
            _libraryRoot = Path.Combine(_tempRoot, "Library");
            var threeDoDir = Path.Combine(_libraryRoot, "3DO");
            Directory.CreateDirectory(_downloadsDir);
            Directory.CreateDirectory(threeDoDir);

            _threeDoPlatform = new Series
            {
                Id = 1,
                Title = "3DO Interactive Multiplayer",
                CleanTitle = "3dointeractivemultiplayer",
                TitleSlug = "3do",
                Path = threeDoDir,
                SeasonFolder = true,
                PreferredArchiveFormat = "auto"
            };

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                .Returns(_libraryRoot);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns((string p) => Directory.Exists(p));

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileExists(It.IsAny<string>()))
                .Returns((string p) => File.Exists(p));

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.CreateFolder(It.IsAny<string>()))
                .Callback((string p) => Directory.CreateDirectory(p));

            Mocker.GetMock<IDiskTransferService>()
                .Setup(s => s.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()))
                .Callback<string, string, TransferMode, bool>((src, dst, mode, overwrite) =>
                {
                    var dstDir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrWhiteSpace(dstDir) && !Directory.Exists(dstDir))
                    {
                        Directory.CreateDirectory(dstDir);
                    }

                    if (mode == TransferMode.Move)
                    {
                        File.Move(src, dst, overwrite);
                    }
                    else
                    {
                        File.Copy(src, dst, overwrite);
                    }
                });

            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildSeasonPath(It.IsAny<Series>(), It.IsAny<int>()))
                .Returns((Series s, int season) => Path.Combine(s.Path, season == 1 ? "USA" : "World"));

            Mocker.GetMock<IImportScript>()
                .Setup(s => s.TryImport(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LocalEpisode>(), It.IsAny<EpisodeFile>(), It.IsAny<TransferMode>()))
                .Returns(ScriptImportDecision.DeferMove);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void should_not_treat_disc_platform_bin_as_zip_compressible()
        {
            RomFileMovingService.IsZipCompressibleRom(".bin", _threeDoPlatform).Should().BeFalse();

            var genesis = new Series { Title = "Sega Genesis", TitleSlug = "genesis" };
            RomFileMovingService.IsZipCompressibleRom(".bin", genesis).Should().BeTrue();
        }

        [Test]
        public void should_compress_3do_cue_and_bin_to_renamed_chd_when_compression_enabled()
        {
            var cuePath = Path.Combine(_downloadsDir, "crash_burn_ia.cue");
            var binPath = Path.Combine(_downloadsDir, "crash_burn_ia.bin");
            File.WriteAllText(cuePath, "FILE \"crash_burn_ia.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            File.WriteAllBytes(binPath, new byte[2048]);

            var expectedDestBase = Path.Combine(_threeDoPlatform.Path, "USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA)");
            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), _threeDoPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => expectedDestBase + ext);

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.BuildCompressionCommand(It.IsAny<string>(), "3do", It.IsAny<string>()))
                .Returns((string src, string slug, string explicitOut) => ("chdman", "createcd", DiscCompressionFormat.ChdCd, Path.ChangeExtension(explicitOut ?? src, ".chd")));

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.CompressDisc(It.IsAny<string>(), "3do", out It.Ref<string>.IsAny, It.IsAny<string>()))
                .Returns((string src, string slug, out string outPath, string explicitOut) =>
                {
                    outPath = explicitOut ?? Path.ChangeExtension(src, ".chd");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    File.WriteAllBytes(outPath, new byte[128]);
                    return true;
                });

            var game = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
            var localGame = new LocalEpisode { Path = cuePath, Series = _threeDoPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = cuePath, RelativePath = "crash_burn_ia.cue" };

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            var expectedChd = expectedDestBase + ".chd";
            File.Exists(expectedChd).Should().BeTrue();
            result.RelativePath.Should().Be(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).chd"));
            File.Exists(cuePath).Should().BeFalse();
            File.Exists(binPath).Should().BeFalse();
        }

        [Test]
        public void should_move_and_rename_cue_and_multitrack_bins_and_rewrite_cue_when_compression_disabled()
        {
            _threeDoPlatform.PreferredArchiveFormat = "none";

            var cuePath = Path.Combine(_downloadsDir, "ia_raw_dump.cue");
            var bin1Path = Path.Combine(_downloadsDir, "track01.bin");
            var bin2Path = Path.Combine(_downloadsDir, "track02.bin");

            File.WriteAllText(
                cuePath,
                "FILE \"track01.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n" +
                "FILE \"track02.bin\" BINARY\r\n  TRACK 02 AUDIO\r\n    INDEX 01 00:00:00\r\n");
            File.WriteAllBytes(bin1Path, new byte[2048]);
            File.WriteAllBytes(bin2Path, new byte[1024]);

            var destDir = Path.Combine(_threeDoPlatform.Path, "USA", "Crash 'n Burn (USA)");
            var expectedCue = Path.Combine(destDir, "Crash 'n Burn (USA).cue");
            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), _threeDoPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => Path.Combine(destDir, "Crash 'n Burn (USA)" + ext));

            var game = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
            var localGame = new LocalEpisode { Path = cuePath, Series = _threeDoPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = cuePath, RelativePath = "ia_raw_dump.cue" };

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            File.Exists(expectedCue).Should().BeTrue();
            var expectedTrack1 = Path.Combine(destDir, "Crash 'n Burn (USA) (Track 1).bin");
            var expectedTrack2 = Path.Combine(destDir, "Crash 'n Burn (USA) (Track 2).bin");
            File.Exists(expectedTrack1).Should().BeTrue();
            File.Exists(expectedTrack2).Should().BeTrue();

            var cueContent = File.ReadAllText(expectedCue);
            cueContent.Should().Contain("FILE \"Crash 'n Burn (USA) (Track 1).bin\" BINARY");
            cueContent.Should().Contain("FILE \"Crash 'n Burn (USA) (Track 2).bin\" BINARY");
            cueContent.Should().NotContain("track01.bin");

            result.RelativePath.Should().Be(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).cue"));
            File.Exists(cuePath).Should().BeFalse();
            File.Exists(bin1Path).Should().BeFalse();
            File.Exists(bin2Path).Should().BeFalse();
        }

        [Test]
        public void should_unwrap_3do_zip_strip_ia_junk_and_compress_to_renamed_chd_when_compression_enabled()
        {
            var zipPath = Path.Combine(_downloadsDir, "Crash.n.Burn.USA.zip");
            Create3doArchiveWithJunk(zipPath);

            var destDir = Path.Combine(_threeDoPlatform.Path, "USA", "Crash 'n Burn (USA)");
            var expectedDestBase = Path.Combine(destDir, "Crash 'n Burn (USA)");
            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), _threeDoPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => expectedDestBase + ext);

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.BuildCompressionCommand(It.IsAny<string>(), "3do", It.IsAny<string>()))
                .Returns((string src, string slug, string explicitOut) => ("chdman", "createcd", DiscCompressionFormat.ChdCd, Path.ChangeExtension(explicitOut ?? src, ".chd")));

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.CompressDisc(It.IsAny<string>(), "3do", out It.Ref<string>.IsAny, It.IsAny<string>()))
                .Returns((string src, string slug, out string outPath, string explicitOut) =>
                {
                    outPath = explicitOut ?? Path.ChangeExtension(src, ".chd");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    File.WriteAllBytes(outPath, new byte[256]);
                    return true;
                });

            var game = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
            var localGame = new LocalEpisode { Path = zipPath, Series = _threeDoPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = zipPath, RelativePath = "Crash.n.Burn.USA.zip" };

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            Directory.GetFiles(destDir).Should().ContainSingle().Which.Should().Be(expectedDestBase + ".chd");
            result.RelativePath.Should().Be(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).chd"));
            File.Exists(zipPath).Should().BeFalse();
        }

        [TestCase(true, Description = "Fallback to uncompressed cue/bin when CompressDisc returns false")]
        [TestCase(false, Description = "Compression disabled -> uncompressed cue/bin")]
        public void should_unwrap_3do_zip_strip_ia_junk_and_import_renamed_cue_bin_when_compression_disabled_or_unavailable(bool compressionEnabledSetting)
        {
            _threeDoPlatform.PreferredArchiveFormat = compressionEnabledSetting ? "auto" : "none";

            var zipPath = Path.Combine(_downloadsDir, "Crash.n.Burn.USA.zip");
            Create3doArchiveWithJunk(zipPath);

            var destDir = Path.Combine(_threeDoPlatform.Path, "USA", "Crash 'n Burn (USA)");
            var expectedDestBase = Path.Combine(destDir, "Crash 'n Burn (USA)");
            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), _threeDoPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => expectedDestBase + ext);

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.BuildCompressionCommand(It.IsAny<string>(), "3do", It.IsAny<string>()))
                .Returns((string src, string slug, string explicitOut) => ("chdman", "createcd", DiscCompressionFormat.ChdCd, Path.ChangeExtension(explicitOut ?? src, ".chd")));

            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.CompressDisc(It.IsAny<string>(), "3do", out It.Ref<string>.IsAny, It.IsAny<string>()))
                .Returns((string src, string slug, out string outPath, string explicitOut) =>
                {
                    outPath = null;
                    return false;
                });

            var game = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
            var localGame = new LocalEpisode { Path = zipPath, Series = _threeDoPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = zipPath, RelativePath = "Crash.n.Burn.USA.zip" };

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            var filesInLibrary = Directory.GetFiles(destDir).Select(Path.GetFileName).OrderBy(n => n).ToList();
            filesInLibrary.Should().BeEquivalentTo(new[]
            {
                "Crash 'n Burn (USA).bin",
                "Crash 'n Burn (USA).cue"
            });

            var cueContent = File.ReadAllText(Path.Combine(destDir, "Crash 'n Burn (USA).cue"));
            cueContent.Should().Contain("FILE \"Crash 'n Burn (USA).bin\" BINARY");
            result.RelativePath.Should().Be(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).cue"));
            File.Exists(zipPath).Should().BeFalse();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_unwrap_cartridge_zip_with_ia_junk_and_repack_clean_renamed_zip_or_extract_uncompressed(bool compressionEnabled)
        {
            var snesDir = Path.Combine(_libraryRoot, "SNES");
            Directory.CreateDirectory(snesDir);
            var snesPlatform = new Series
            {
                Id = 2,
                Title = "Super Nintendo",
                TitleSlug = "snes",
                Path = snesDir,
                SeasonFolder = true,
                PreferredArchiveFormat = compressionEnabled ? "zip" : "none"
            };

            var zipPath = Path.Combine(_downloadsDir, "raw_ia_upload.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using (var s = zip.CreateEntry("subfolder/Chrono.sfc").Open())
                {
                    s.Write(new byte[] { 10, 20, 30, 40 });
                }

                using (var s = zip.CreateEntry("item_meta.xml").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("<metadata/>"));
                }

                using (var s = zip.CreateEntry("readme.txt").Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("junk"));
                }
            }

            var destDir = Path.Combine(snesDir, "USA");
            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), snesPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => Path.Combine(destDir, "Chrono Trigger (USA)" + ext));

            var game = new Episode { Id = 2, SeasonNumber = 1, EpisodeNumber = 1, Title = "Chrono Trigger" };
            var localGame = new LocalEpisode { Path = zipPath, Series = snesPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = zipPath, RelativePath = "raw_ia_upload.zip" };

            var result = Subject.MoveEpisodeFile(romFile, localGame);

            if (compressionEnabled)
            {
                var expectedZip = Path.Combine(destDir, "Chrono Trigger (USA).zip");
                File.Exists(expectedZip).Should().BeTrue();
                result.RelativePath.Should().Be(Path.Combine("USA", "Chrono Trigger (USA).zip"));

                using var opened = ZipFile.OpenRead(expectedZip);
                opened.Entries.Should().ContainSingle();
                opened.Entries[0].FullName.Should().Be("Chrono Trigger (USA).sfc");
            }
            else
            {
                var expectedSfc = Path.Combine(destDir, "Chrono Trigger (USA).sfc");
                Directory.GetFiles(destDir).Should().ContainSingle().Which.Should().Be(expectedSfc);
                result.RelativePath.Should().Be(Path.Combine("USA", "Chrono Trigger (USA).sfc"));
            }

            File.Exists(zipPath).Should().BeFalse();
        }

        [Test]
        public void should_group_only_matching_multidisc_titles_for_m3u_playlist_in_flat_region_folder()
        {
            _threeDoPlatform.PreferredArchiveFormat = "none";

            var usaRegionDir = Path.Combine(_threeDoPlatform.Path, "USA");
            Directory.CreateDirectory(usaRegionDir);

            // An unrelated game in the same flat region folder and Disc 1 of the target multi-disc game
            var unrelatedChd = Path.Combine(usaRegionDir, "Gex (USA).chd");
            var disc1Chd = Path.Combine(usaRegionDir, "D (USA) (Disc 1).chd");
            File.WriteAllBytes(unrelatedChd, new byte[64]);
            File.WriteAllBytes(disc1Chd, new byte[64]);

            var srcDisc2Chd = Path.Combine(_downloadsDir, "D_USA_Disc2.chd");
            File.WriteAllBytes(srcDisc2Chd, new byte[64]);

            Mocker.GetMock<IBuildFileNames>()
                .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), _threeDoPlatform, It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<CustomFormat>>()))
                .Returns((List<Episode> eps, Series ser, EpisodeFile ef, string ext, NamingConfig cfg, List<CustomFormat> cfs) => Path.Combine(usaRegionDir, "D (USA) (Disc 2)" + ext));

            var generatedPlaylists = new List<(string[] Paths, string M3uPath)>();
            Mocker.GetMock<IDiscCompressionService>()
                .Setup(s => s.GenerateM3uPlaylist(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((paths, m3uPath) => generatedPlaylists.Add((paths.ToArray(), m3uPath)));

            var game = new Episode { Id = 9, SeasonNumber = 1, EpisodeNumber = 2, Title = "D" };
            var localGame = new LocalEpisode { Path = srcDisc2Chd, Series = _threeDoPlatform, Episodes = new List<Episode> { game } };
            var romFile = new EpisodeFile { Path = srcDisc2Chd, RelativePath = "D_USA_Disc2.chd" };

            Subject.MoveEpisodeFile(romFile, localGame);

            generatedPlaylists.Should().ContainSingle();
            generatedPlaylists[0].M3uPath.Should().Be(Path.Combine(usaRegionDir, "D (USA).m3u"));
            generatedPlaylists[0].Paths.Select(Path.GetFileName).Should().BeEquivalentTo(new[]
            {
                "D (USA) (Disc 1).chd",
                "D (USA) (Disc 2).chd"
            });
        }

        private static void Create3doArchiveWithJunk(string zipPath)
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            using (var s = zip.CreateEntry("inner/Raw Dump.cue").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("FILE \"Raw Dump.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n"));
            }

            using (var s = zip.CreateEntry("inner/Raw Dump.bin").Open())
            {
                s.Write(new byte[2048]);
            }

            using (var s = zip.CreateEntry("raw_meta.xml").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("<metadata/>"));
            }

            using (var s = zip.CreateEntry("raw_files.xml").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("<files/>"));
            }

            using (var s = zip.CreateEntry("readme.txt").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("IA readme"));
            }

            using (var s = zip.CreateEntry("cover.jpg").Open())
            {
                s.Write(new byte[] { 0xFF, 0xD8, 0xFF });
            }
        }
    }
}
