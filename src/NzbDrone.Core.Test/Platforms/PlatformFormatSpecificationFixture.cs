using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.GameImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Platforms
{
    [TestFixture]
    public class PlatformFormatSpecificationFixture : CoreTest<PlatformFormatSpecification>
    {
        private LocalEpisode _localEpisode;
        private string _testFilePath;

        [SetUp]
        public void Setup()
        {
            _testFilePath = Path.GetTempFileName();
            _localEpisode = new LocalEpisode
            {
                Path = _testFilePath
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        [Test]
        public void should_reject_when_file_is_under_1kb()
        {
            // Write a tiny amount of bytes (stub)
            File.WriteAllBytes(_testFilePath, new byte[500]);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_file_is_over_1kb_and_allowed()
        {
            // Write larger than 1kb
            File.WriteAllBytes(_testFilePath, new byte[2048]);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_file_matches_platform_exclusive_extension()
        {
            File.WriteAllBytes(_testFilePath, new byte[2048]);
            var pathWithExt = _testFilePath + ".3ds";
            File.Move(_testFilePath, pathWithExt);
            _testFilePath = pathWithExt;

            _localEpisode.Path = pathWithExt;
            _localEpisode.Series = new Series
            {
                Title = "Nintendo 3DS",
                TitleSlug = "3ds"
            };

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_file_does_not_match_platform_extension()
        {
            File.WriteAllBytes(_testFilePath, new byte[2048]);
            var pathWithExt = _testFilePath + ".iso";
            File.Move(_testFilePath, pathWithExt);
            _testFilePath = pathWithExt;

            _localEpisode.Path = pathWithExt;
            _localEpisode.Series = new Series
            {
                Title = "Nintendo 3DS",
                TitleSlug = "3ds"
            };

            var decision = Subject.IsSatisfiedBy(_localEpisode, null);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.PlatformFormatRejected);
        }

        [Test]
        public void should_accept_small_cue_sheet_when_referenced_bin_track_exceeds_1kb()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pfs_cue_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var cuePath = Path.Combine(tempDir, "Crash 'n Burn (USA).cue");
                var binPath = Path.Combine(tempDir, "Crash 'n Burn (USA).bin");

                File.WriteAllText(cuePath, "FILE \"Crash 'n Burn (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
                File.WriteAllBytes(binPath, new byte[4096]);

                _localEpisode.Path = cuePath;
                _localEpisode.Series = new Series { Title = "3DO Interactive Multiplayer", TitleSlug = "3do" };

                Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
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
        public void should_reject_orphan_small_cue_sheet_without_bin_track()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pfs_orphan_cue_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var cuePath = Path.Combine(tempDir, "Orphan (USA).cue");
                File.WriteAllText(cuePath, "FILE \"Missing.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");

                _localEpisode.Path = cuePath;
                _localEpisode.Series = new Series { Title = "3DO Interactive Multiplayer", TitleSlug = "3do" };

                var decision = Subject.IsSatisfiedBy(_localEpisode, null);
                decision.Accepted.Should().BeFalse();
                decision.Reason.Should().Be(ImportRejectionReason.PlatformFormatRejected);
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
        public void should_reject_archive_containing_only_junk_files()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pfs_junk_zip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var zipPath = Path.Combine(tempDir, "Junk (USA).zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    using (var s = archive.CreateEntry("item_meta.xml").Open())
                    {
                        s.Write(Encoding.UTF8.GetBytes(new string('a', 1500)));
                    }

                    using (var s = archive.CreateEntry("readme.txt").Open())
                    {
                        s.Write(Encoding.UTF8.GetBytes(new string('b', 1500)));
                    }
                }

                _localEpisode.Path = zipPath;
                _localEpisode.Series = new Series { Title = "3DO Interactive Multiplayer", TitleSlug = "3do" };

                var decision = Subject.IsSatisfiedBy(_localEpisode, null);
                decision.Accepted.Should().BeFalse();
                decision.Reason.Should().Be(ImportRejectionReason.PlatformFormatRejected);
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
