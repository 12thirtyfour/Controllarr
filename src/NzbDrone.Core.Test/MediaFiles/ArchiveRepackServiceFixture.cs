using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ArchiveRepackServiceFixture : CoreTest<ArchiveRepackService>
    {
        private string _archivePath;

        [SetUp]
        public void Setup()
        {
            _archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_archivePath))
            {
                File.Delete(_archivePath);
            }
        }

        [Test]
        public void should_detect_and_strip_512_byte_smc_header()
        {
            // Given: a zip with an .smc file that has a 512-byte header + some fake data
            var fakeRomData = new byte[1024 + 512]; // Divisible by 1024, remainder 512

            using (var zip = ZipFile.Open(_archivePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("game.smc");
                using (var stream = entry.Open())
                {
                    stream.Write(fakeRomData, 0, fakeRomData.Length);
                }
            }

            // When
            Subject.RepackArchive(_archivePath);

            // Then: The archive should now contain a .sfc file of 1024 bytes
            using (var zip = ZipFile.OpenRead(_archivePath))
            {
                zip.Entries.Should().HaveCount(1);
                var newEntry = zip.Entries[0];
                newEntry.Name.Should().Be("game.sfc");
                newEntry.Length.Should().Be(1024);
            }
        }

        [Test]
        public void should_detect_ines_header()
        {
            var headerBytes = new byte[] { (byte)'N', (byte)'E', (byte)'S', 0x1A, 0x01, 0x02 };
            var result = Subject.ModernizeINesHeader(headerBytes);

            // For now our modernization method just returns the byte array unharmed,
            // we're just testing it parses correctly without crashing
            result.Should().BeEquivalentTo(headerBytes);
        }

        [Test]
        public void should_sync_single_rom_entry_name_to_match_renamed_archive()
        {
            var renamedArchivePath = Path.Combine(Path.GetTempPath(), "Super Mario Land.zip");
            if (File.Exists(renamedArchivePath))
            {
                File.Delete(renamedArchivePath);
            }

            try
            {
                using (var zip = ZipFile.Open(renamedArchivePath, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("Super Mario Land (USA).gb");
                    using var stream = entry.Open();
                    stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                }

                Subject.SyncSingleRomEntryName(renamedArchivePath);

                using (var zip = ZipFile.OpenRead(renamedArchivePath))
                {
                    zip.Entries.Should().HaveCount(1);
                    zip.Entries[0].FullName.Should().Be("Super Mario Land.gb");
                }
            }
            finally
            {
                if (File.Exists(renamedArchivePath))
                {
                    File.Delete(renamedArchivePath);
                }
            }
        }

        [Test]
        public void should_unwrap_nested_archive_and_strip_junk_files_to_single_rom_entry()
        {
            var outerArchivePath = Path.Combine(Path.GetTempPath(), "Chrono Trigger (USA).zip");
            var innerZipPath = Path.Combine(Path.GetTempPath(), "nested_" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                using (var inner = ZipFile.Open(innerZipPath, ZipArchiveMode.Create))
                {
                    var romEntry = inner.CreateEntry("subfolder/Chrono Trigger.sfc");
                    using (var s = romEntry.Open())
                    {
                        s.Write(new byte[] { 10, 20, 30, 40 }, 0, 4);
                    }

                    var innerJunk = inner.CreateEntry("readme.txt");
                    using (var s = innerJunk.Open())
                    {
                        s.Write(new byte[] { 1, 2 }, 0, 2);
                    }
                }

                using (var outer = ZipFile.Open(outerArchivePath, ZipArchiveMode.Create))
                {
                    outer.CreateEntryFromFile(innerZipPath, "Chrono Trigger.zip");

                    var outerJunk = outer.CreateEntry("site_info.nfo");
                    using (var s = outerJunk.Open())
                    {
                        s.Write(new byte[] { 3, 4 }, 0, 2);
                    }

                    var macJunk = outer.CreateEntry("__MACOSX/._Chrono Trigger.zip");
                    using (var s = macJunk.Open())
                    {
                        s.Write(new byte[] { 5, 6 }, 0, 2);
                    }
                }

                Subject.SyncSingleRomEntryName(outerArchivePath);

                using (var zip = ZipFile.OpenRead(outerArchivePath))
                {
                    zip.Entries.Should().HaveCount(1);
                    zip.Entries[0].FullName.Should().Be("Chrono Trigger (USA).sfc");
                }
            }
            finally
            {
                if (File.Exists(outerArchivePath))
                {
                    File.Delete(outerArchivePath);
                }

                if (File.Exists(innerZipPath))
                {
                    File.Delete(innerZipPath);
                }
            }
        }

        [Test]
        public void should_strip_junk_files_when_zip_contains_one_rom_and_extra_files()
        {
            var archivePath = Path.Combine(Path.GetTempPath(), "Metroid (USA).zip");

            try
            {
                using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    var romEntry = zip.CreateEntry("roms/Metroid.nes");
                    using (var s = romEntry.Open())
                    {
                        s.Write(new byte[] { 7, 8, 9 }, 0, 3);
                    }

                    var txtEntry = zip.CreateEntry("readme.txt");
                    using (var s = txtEntry.Open())
                    {
                        s.Write(new byte[] { 1, 2 }, 0, 2);
                    }

                    var thumbsEntry = zip.CreateEntry("Thumbs.db");
                    using (var s = thumbsEntry.Open())
                    {
                        s.Write(new byte[] { 1, 2 }, 0, 2);
                    }
                }

                Subject.SyncSingleRomEntryName(archivePath);

                using (var zip = ZipFile.OpenRead(archivePath))
                {
                    zip.Entries.Should().HaveCount(1);
                    zip.Entries[0].FullName.Should().Be("Metroid (USA).nes");
                }
            }
            finally
            {
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
            }
        }
    }
}
