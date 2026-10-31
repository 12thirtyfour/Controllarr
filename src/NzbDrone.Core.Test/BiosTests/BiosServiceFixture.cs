using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.BiosTests
{
    [TestFixture]
    public class BiosServiceFixture : CoreTest<BiosService>
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ControllarrBiosTests_" + Guid.NewGuid().ToString("N"));
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
        public void Should_return_canonical_bios_catalog_filtered_by_platform()
        {
            var ps1Bios = Subject.GetCatalog("ps1");
            ps1Bios.Should().HaveCount(3);
            ps1Bios.Select(b => b.FileName).Should().Contain(new[] { "scph5500.bin", "scph5501.bin", "scph5502.bin" });
        }

        [Test]
        public void Should_verify_bios_file_against_expected_md5_and_sha1()
        {
            var payload = Encoding.UTF8.GetBytes("test_bios_payload");
            var biosPath = Path.Combine(_tempDir, "test_bios.bin");
            File.WriteAllBytes(biosPath, payload);

            string expectedMd5;
            string expectedSha1;
            using (var md5 = MD5.Create())
            using (var sha1 = SHA1.Create())
            {
                expectedMd5 = BitConverter.ToString(md5.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                expectedSha1 = BitConverter.ToString(sha1.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
            }

            Subject.VerifyBios(biosPath, expectedMd5, expectedSha1).Should().BeTrue();
            Subject.VerifyBios(biosPath, "bad_md5", expectedSha1).Should().BeFalse();
        }

        [Test]
        public void Should_strictly_isolate_addon_bios_files_from_base_consoles()
        {
            Subject.GetCatalog("nes").Should().BeEmpty();
            Subject.GetCatalog("fds").Select(b => b.FileName).Should().Contain("disksys.rom");

            Subject.GetCatalog("snes").Should().BeEmpty();
            Subject.GetCatalog("sgb").Select(b => b.FileName).Should().Contain("sgb_bios.bin");
            Subject.GetCatalog("satellaview").Select(b => b.FileName).Should().Contain("BS-X.bin");

            Subject.GetCatalog("genesis").Should().BeEmpty();
            Subject.GetCatalog("segacd").Select(b => b.FileName).Should().Contain("bios_CD_U.bin");

            Subject.GetCatalog("tg16").Should().BeEmpty();
            Subject.GetCatalog("tgcd").Select(b => b.FileName).Should().Contain("syscard3.pce");

            Subject.GetCatalog("jaguar").Should().BeEmpty();
            Subject.GetCatalog("jaguarcd").Select(b => b.FileName).Should().Contain("jagcd.rom");

            Subject.GetCatalog("n64").Should().BeEmpty();
            Subject.GetCatalog("64dd").Select(b => b.FileName).Should().Contain("64DD_IPL.bin");
        }

        [Test]
        public void Should_discard_invalid_bios_file_when_scanning_directory()
        {
            var corruptBiosPath = Path.Combine(_tempDir, "disksys.rom");
            File.WriteAllBytes(corruptBiosPath, Encoding.UTF8.GetBytes("corrupted_fds_payload"));

            var results = Subject.ScanAndVerifyBiosDirectory(_tempDir, "fds");

            File.Exists(corruptBiosPath).Should().BeFalse("corrupt BIOS file must be discarded from disk");
            var fdsBios = results.FirstOrDefault(b => b.FileName == "disksys.rom");
            fdsBios.Should().NotBeNull();
            fdsBios.IsVerified.Should().BeFalse();
            fdsBios.Status.Should().Be(2);
        }

        [Test]
        public void Should_discard_downloaded_payload_if_it_fails_hash_verification()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            var badPayload = Encoding.UTF8.GetBytes("bad_server_payload");
            var mockResponse = new HttpResponse(new HttpRequest("https://test.example/disksys.rom"), new HttpHeader(), badPayload);

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get(It.IsAny<HttpRequest>()))
                  .Returns(mockResponse);

            var results = Subject.DownloadMissingBios("fds", "disksys.rom");

            var expectedDestination = Path.Combine(_tempDir, "bios", "fds", "disksys.rom");
            File.Exists(expectedDestination).Should().BeFalse("unverified downloaded payload must be discarded from disk");

            var fdsBios = results.FirstOrDefault(b => b.FileName == "disksys.rom");
            fdsBios.Should().NotBeNull();
            fdsBios.IsVerified.Should().BeFalse();
        }

        [Test]
        public void Should_discard_invalid_existing_file_in_platform_bios_directory_on_catalog_check()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            var biosDir = Path.Combine(_tempDir, "bios", "fds");
            Directory.CreateDirectory(biosDir);
            var corruptPath = Path.Combine(biosDir, "disksys.rom");
            File.WriteAllBytes(corruptPath, Encoding.UTF8.GetBytes("corrupted_payload"));

            var catalog = Subject.GetCatalog("fds");

            File.Exists(corruptPath).Should().BeFalse("existing corrupt BIOS file must be discarded upon hash verification failure");
            var entry = catalog.FirstOrDefault(b => b.FileName == "disksys.rom");
            entry.Should().NotBeNull();
            entry.IsVerified.Should().BeFalse();
            entry.Status.Should().Be(2);
        }

        [Test]
        public void Should_extract_and_discard_downloaded_zip_payload_if_extracted_file_fails_hash()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            byte[] zipBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("disksys.rom");
                    using (var entryStream = entry.Open())
                    {
                        var data = Encoding.UTF8.GetBytes("invalid_inner_bios_data");
                        entryStream.Write(data, 0, data.Length);
                    }
                }

                zipBytes = memoryStream.ToArray();
            }

            var mockResponse = new HttpResponse(new HttpRequest("https://test.example/disksys.rom.zip"), new HttpHeader(), zipBytes);

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get(It.IsAny<HttpRequest>()))
                  .Returns(mockResponse);

            var results = Subject.DownloadMissingBios("fds", "disksys.rom");

            var expectedDestination = Path.Combine(_tempDir, "bios", "fds", "disksys.rom");
            File.Exists(expectedDestination).Should().BeFalse("unverified extracted payload must be discarded from disk");

            var fdsBios = results.FirstOrDefault(b => b.FileName == "disksys.rom");
            fdsBios.Should().NotBeNull();
            fdsBios.IsVerified.Should().BeFalse();
        }

        [Test]
        public void Should_have_valid_cryptographic_hashes_for_all_canonical_bios_catalog_entries()
        {
            var catalog = Subject.GetCatalog();
            catalog.Should().NotBeEmpty();

            foreach (var entry in catalog)
            {
                entry.ExpectedMd5.Should().NotBeNullOrWhiteSpace($"{entry.GamePlatform}/{entry.FileName} must define ExpectedMd5");
                entry.ExpectedMd5.Should().MatchRegex("^[0-9a-f]{32}$", $"{entry.GamePlatform}/{entry.FileName} ExpectedMd5 must be 32 hex chars");
                entry.ExpectedSha1.Should().NotBeNullOrWhiteSpace($"{entry.GamePlatform}/{entry.FileName} must define ExpectedSha1");
                entry.ExpectedSha1.Should().MatchRegex("^[0-9a-f]{40}$", $"{entry.GamePlatform}/{entry.FileName} ExpectedSha1 must be 40 hex chars");
                entry.ExpectedSha1.Should().NotContain("a1b2c3d4e5f6", $"{entry.GamePlatform}/{entry.FileName} must not contain placeholder SHA1");
            }

            var segaCdJp = catalog.FirstOrDefault(b => b.GamePlatform == "segacd" && b.FileName == "bios_CD_J.bin");
            segaCdJp.Should().NotBeNull();
            segaCdJp.ExpectedMd5.Should().Be("278a9397d192149e84e820ac621a8edd");
            segaCdJp.ExpectedSha1.Should().Be("4846f448160059a7da0215a5df12ca160f26dd69");
        }

        [Test]
        public void Should_verify_byteswapped_sega_cd_bios_dump()
        {
            var linearRom = new byte[131072];
            for (var i = 0; i < linearRom.Length; i++)
            {
                linearRom[i] = (byte)((i * 31) & 0xFF);
            }

            string linearMd5;
            string linearSha1;
            using (var md5 = MD5.Create())
            using (var sha1 = SHA1.Create())
            {
                linearMd5 = BitConverter.ToString(md5.ComputeHash(linearRom)).Replace("-", "").ToLowerInvariant();
                linearSha1 = BitConverter.ToString(sha1.ComputeHash(linearRom)).Replace("-", "").ToLowerInvariant();
            }

            var swappedRom = new byte[131072];
            for (var i = 0; i < linearRom.Length - 1; i += 2)
            {
                swappedRom[i] = linearRom[i + 1];
                swappedRom[i + 1] = linearRom[i];
            }

            var biosPath = Path.Combine(_tempDir, "bios_CD_J.bin");
            File.WriteAllBytes(biosPath, swappedRom);

            Subject.VerifyBios(biosPath, linearMd5, linearSha1).Should().BeTrue();
        }
    }
}
