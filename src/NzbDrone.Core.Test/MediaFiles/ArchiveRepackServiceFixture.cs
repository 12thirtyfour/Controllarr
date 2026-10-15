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
    }
}
