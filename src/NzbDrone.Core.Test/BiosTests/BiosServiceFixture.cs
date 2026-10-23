using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
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
    }
}
