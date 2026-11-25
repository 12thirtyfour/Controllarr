using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Dat;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

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
        public void Should_mark_invalid_existing_file_as_mismatched_without_deleting_on_catalog_check()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            var biosDir = Path.Combine(_tempDir, "bios", "fds");
            Directory.CreateDirectory(biosDir);
            var corruptPath = Path.Combine(biosDir, "disksys.rom");
            File.WriteAllBytes(corruptPath, Encoding.UTF8.GetBytes("corrupted_payload"));

            var catalog = Subject.GetCatalog("fds");

            File.Exists(corruptPath).Should().BeTrue("read-only GetCatalog() must not delete existing files from disk");
            Mocker.GetMock<IHttpClient>().Verify(c => c.Get(It.IsAny<HttpRequest>()), Times.Never());
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
        public void Should_continue_searching_other_sources_when_earlier_bios_candidate_fails_hash_check()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            var attemptedUrls = new List<string>();
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(req =>
                  {
                      var url = req.Url.ToString();
                      attemptedUrls.Add(url);

                      if (url.Contains("archive.org/advancedsearch.php"))
                      {
                          var searchJson = "{\"response\":{\"docs\":[{\"identifier\":\"bad-pack\"},{\"identifier\":\"good-pack\"}]}}";
                          return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(searchJson));
                      }

                      if (url.Contains("archive.org/metadata/bad-pack/files"))
                      {
                          var badMeta = "{\"result\":[{\"name\":\"disksys.rom\",\"md5\":\"00000000000000000000000000000000\",\"sha1\":\"0000000000000000000000000000000000000000\"}]}";
                          return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(badMeta));
                      }

                      if (url.Contains("archive.org/metadata/good-pack/files"))
                      {
                          var goodMeta = "{\"result\":[{\"name\":\"disksys.rom\",\"md5\":\"ca30b50f880eb660a320674ed365ef7a\",\"sha1\":\"57fe1bdee955bb48d357e463ccbf129496930b62\"}]}";
                          return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(goodMeta));
                      }

                      return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes("bad_payload"));
                  });

            Subject.DownloadMissingBios("fds", "disksys.rom");

            attemptedUrls.Should().Contain(u => u.Contains("archive.org/advancedsearch.php") && u.Contains("rows=15"));
            attemptedUrls.Should().NotContain(u => u.Contains("/download/bad-pack/"), "metadata hash mismatch should be skipped without downloading");
            attemptedUrls.Should().Contain(u => u.Contains("/download/good-pack/disksys.rom"), "matching metadata hash should be tried");
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
                if (!string.IsNullOrWhiteSpace(entry.ExpectedSha1))
                {
                    entry.ExpectedSha1.Should().MatchRegex("^[0-9a-f]{40}$", $"{entry.GamePlatform}/{entry.FileName} ExpectedSha1 must be 40 hex chars");
                    entry.ExpectedSha1.Should().NotContain("a1b2c3d4e5f6", $"{entry.GamePlatform}/{entry.FileName} must not contain placeholder SHA1");
                }
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

        [Test]
        public void Should_process_and_import_candidate_bios_file_into_series_bios_directory()
        {
            var seriesDir = Path.Combine(_tempDir, "Sega CD");
            Directory.CreateDirectory(seriesDir);

            var series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Title = "Sega CD",
                TitleSlug = "segacd",
                Path = seriesDir
            };

            var downloadSource = Path.Combine(_tempDir, "downloads", "bios_CD_U.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(downloadSource));
            var payload = Encoding.UTF8.GetBytes("fake_segacd_bios_payload");
            File.WriteAllBytes(downloadSource, payload);

            var segaCdEntry = BiosService.CanonicalBiosCatalog.First(b => b.GamePlatform == "segacd" && b.FileName == "bios_CD_U.bin");
            var originalMd5 = segaCdEntry.ExpectedMd5;
            var originalSha1 = segaCdEntry.ExpectedSha1;

            try
            {
                using (var md5 = MD5.Create())
                using (var sha1 = SHA1.Create())
                {
                    segaCdEntry.ExpectedMd5 = BitConverter.ToString(md5.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                    segaCdEntry.ExpectedSha1 = BitConverter.ToString(sha1.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                }

                Subject.IsKnownBiosFileName("bios_CD_U.bin").Should().BeTrue();
                Subject.IsKnownBiosFileName("unknown_game.bin").Should().BeFalse();

                var result = Subject.ProcessCandidateBiosFile(downloadSource, series, move: true);
                result.Should().BeTrue();

                var expectedBiosPath = Path.Combine(seriesDir, "BIOS", "bios_CD_U.bin");
                File.Exists(expectedBiosPath).Should().BeTrue();
                File.Exists(downloadSource).Should().BeFalse("moved source should no longer exist");
            }
            finally
            {
                segaCdEntry.ExpectedMd5 = originalMd5;
                segaCdEntry.ExpectedSha1 = originalSha1;
            }
        }

        [Test]
        public void Should_scan_and_move_loose_bios_file_into_series_bios_directory()
        {
            var seriesDir = Path.Combine(_tempDir, "Sega CD");
            Directory.CreateDirectory(seriesDir);

            var series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Title = "Sega CD",
                TitleSlug = "segacd",
                Path = seriesDir
            };

            var looseFile = Path.Combine(seriesDir, "bios_CD_U.bin");
            var payload = Encoding.UTF8.GetBytes("loose_bios_payload");
            File.WriteAllBytes(looseFile, payload);

            var segaCdEntry = BiosService.CanonicalBiosCatalog.First(b => b.GamePlatform == "segacd" && b.FileName == "bios_CD_U.bin");
            var originalMd5 = segaCdEntry.ExpectedMd5;
            var originalSha1 = segaCdEntry.ExpectedSha1;

            try
            {
                using (var md5 = MD5.Create())
                using (var sha1 = SHA1.Create())
                {
                    segaCdEntry.ExpectedMd5 = BitConverter.ToString(md5.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                    segaCdEntry.ExpectedSha1 = BitConverter.ToString(sha1.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                }

                Subject.ScanAndVerifyPlatformBios(series);

                var expectedBiosPath = Path.Combine(seriesDir, "BIOS", "bios_CD_U.bin");
                File.Exists(expectedBiosPath).Should().BeTrue();
                File.Exists(looseFile).Should().BeFalse("loose file in series root must be moved into BIOS subfolder");
            }
            finally
            {
                segaCdEntry.ExpectedMd5 = originalMd5;
                segaCdEntry.ExpectedSha1 = originalSha1;
            }
        }

        [Test]
        public void Should_include_switch_3ds_ps3_psvita_and_psp_firmware_entries()
        {
            var switchCatalog = Subject.GetCatalog("switch");
            switchCatalog.Count.Should().BeGreaterThan(100);
            switchCatalog[0].FileName.Should().Be("Firmware 23.0.1.zip");
            switchCatalog[1].FileName.Should().Be("Firmware 23.0.0.zip");
            switchCatalog.Should().Contain(b => b.FileName == "Firmware 23.0.1.zip" && b.ExpectedMd5 == "910bd0b523553c2a97254b654392976d");
            switchCatalog.Should().Contain(b => b.FileName == "Firmware 1.0.0-7 (Pre-Release).zip");
            switchCatalog.Should().Contain(b => b.FileName == "Firmware 19.0.2 (China).zip");

            var threeDsCatalog = Subject.GetCatalog("3ds");
            threeDsCatalog.Count.Should().BeGreaterThan(100);
            threeDsCatalog[0].FileName.Should().Be("11.17.0-50U.zip");
            threeDsCatalog.Should().Contain(b => b.FileName == "11.17.0-50U.zip" && b.ExpectedMd5 == "e2f4d95f4fd8009d6785808f0e7a1749");
            threeDsCatalog.Should().Contain(b => b.FileName == "11.17.0-50E.zip" && b.ExpectedMd5 == "d5357f47ead9fb98fb7d2522327693f8");
            threeDsCatalog.Should().Contain(b => b.FileName == "11.17.0-50J.zip" && b.ExpectedMd5 == "b2fb9eed4fb1fdf82c60ff507afa9fe2");
            threeDsCatalog.Should().Contain(b => b.FileName == "1.0.0-0U.zip");

            var ps3Catalog = Subject.GetCatalog("ps3");
            ps3Catalog.Should().Contain(b => b.FileName == "PS3UPDAT.PUP" && b.ExpectedMd5 == "a2b60b6ac1d2e49e230144345616927c");

            var vitaCatalog = Subject.GetCatalog("psvita");
            var vitaFirmware = vitaCatalog.FirstOrDefault(b => b.FileName == "PSP2UPDAT.PUP");
            vitaFirmware.Should().NotBeNull();
            vitaFirmware.ExpectedMd5.Should().Be("f2c7b12fe85496ec88a0391b514d6e3b");
            vitaFirmware.DownloadUrl.Should().StartWith("https://archive.org/download/PSVita-Official-Firmwares/");

            var pspCatalog = Subject.GetCatalog("psp");
            pspCatalog.Count.Should().BeGreaterThan(60);
            pspCatalog[0].FileName.Should().Be("661.PBP");
            pspCatalog[1].FileName.Should().Be("660.PBP");
            pspCatalog.Should().Contain(b => b.FileName == "661.PBP" && b.DownloadUrl.StartsWith("https://archive.org/download/psp_ofw_firmwares/"));
            pspCatalog.Should().Contain(b => b.FileName == "660.PBP" && b.DownloadUrl.StartsWith("https://archive.org/download/psp_ofw_firmwares/"));
        }

        [Test]
        public void Should_parse_and_sync_newer_firmwares_from_darthsternie_html()
        {
            var sampleSwitchHtml = @"
<table>
  <tr class=""row-1""><th class=""column-1"">Version</th><th class=""column-2"">MD5 Hash</th><th class=""column-3"">Filesize</th><th class=""column-4"">MEGA</th><th class=""column-5"">Archive.org</th></tr>
  <tr class=""row-2""><td class=""column-1"">Firmware 23.0.1</td><td class=""column-2"">910bd0b523553c2a97254b654392976d</td><td class=""column-3"">324MB</td><td class=""column-4""><a href=""https://mega.nz/file/3RcBAAzZ#OGaAmVgJwKs9ByoNLsAFhO8leOu-IaJouLwuZIH-dhQ"">Download</a></td><td class=""column-5""></td></tr>
</table>";

            var parsed = DarthsternieFirmwareArchive.ParseFirmwareRows("switch", sampleSwitchHtml);
            parsed.Should().HaveCount(1);
            parsed[0].FileName.Should().Be("Firmware 23.0.1.zip");
            parsed[0].ExpectedMd5.Should().Be("910bd0b523553c2a97254b654392976d");
            parsed[0].DownloadUrls.Should().Contain("https://mega.nz/file/3RcBAAzZ#OGaAmVgJwKs9ByoNLsAFhO8leOu-IaJouLwuZIH-dhQ");

            var httpClientMock = new Mock<NzbDrone.Common.Http.IHttpClient>();
            httpClientMock
                .Setup(c => c.Get(It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.ToString().Contains("switch-firmwares"))))
                .Returns<NzbDrone.Common.Http.HttpRequest>(req => new NzbDrone.Common.Http.HttpResponse(
                    req,
                    new NzbDrone.Common.Http.HttpHeader(),
                    sampleSwitchHtml,
                    System.Net.HttpStatusCode.OK));

            var service = new BiosService(
                NLog.LogManager.GetCurrentClassLogger(),
                httpClient: httpClientMock.Object);

            var synced = service.SyncFirmwareUpdates(new[] { "switch" });
            synced.Should().Be(1);

            var switchCatalog = service.GetCatalog("switch");
            switchCatalog.Should().Contain(b =>
                b.FileName == "Firmware 23.0.1.zip" &&
                b.ExpectedMd5 == "910bd0b523553c2a97254b654392976d" &&
                b.DownloadUrl == "https://mega.nz/file/3RcBAAzZ#OGaAmVgJwKs9ByoNLsAFhO8leOu-IaJouLwuZIH-dhQ");
        }

        [Test]
        public void Should_populate_switch_23_0_1_download_url_by_default_and_sync_darthsternie_on_demand_when_missing()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            var defaultSwitch = Subject.GetCatalog("switch");
            defaultSwitch.Should().Contain(b =>
                b.FileName == "Firmware 23.0.1.zip" &&
                b.DownloadUrl == "https://mega.nz/file/3RcBAAzZ#OGaAmVgJwKs9ByoNLsAFhO8leOu-IaJouLwuZIH-dhQ");

            var fetchedSwitchFirmwares = false;
            var sampleHtml = @"
<table>
  <tr class=""row-1""><th class=""column-1"">Version</th><th class=""column-2"">MD5 Hash</th><th class=""column-3"">Filesize</th><th class=""column-4"">MEGA</th><th class=""column-5"">Archive.org</th></tr>
  <tr class=""row-2""><td class=""column-1"">Firmware 23.0.0</td><td class=""column-2"">e9f3a9cdf8d545ab97c29f2f0d33a512</td><td class=""column-3"">324MB</td><td class=""column-4""></td><td class=""column-5""><a href=""https://archive.org/download/test-switch-fw/Firmware%2023.0.0.zip"">Download</a></td></tr>
</table>";

            var attemptedUrls = new List<string>();
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(req =>
                  {
                      var url = req.Url.ToString();
                      attemptedUrls.Add(url);

                      if (url.Contains("switch-firmwares"))
                      {
                          fetchedSwitchFirmwares = true;
                          return new HttpResponse(req, new HttpHeader(), sampleHtml, System.Net.HttpStatusCode.OK);
                      }

                      return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes("bad_payload"));
                  });

            Subject.DownloadMissingBios("switch", "Firmware 23.0.0.zip");

            fetchedSwitchFirmwares.Should().BeTrue("Darthsternie firmware index should be synced on demand when candidate URLs are empty");
            attemptedUrls.Should().Contain("https://archive.org/download/test-switch-fw/Firmware%2023.0.0.zip");
        }

        [Test]
        public void Should_populate_verified_download_urls_for_jagcd_dc_flash_and_scph39001()
        {
            var jagCd = Subject.GetCatalog("jaguarcd").FirstOrDefault(b => b.FileName == "jagcd.rom");
            jagCd.Should().NotBeNull();
            jagCd.DownloadUrl.Should().Be("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/Atari/Jaguar/%5BBIOS%5D%20Atari%20Jaguar%20CD%20(World).j64");

            var dcFlash = Subject.GetCatalog("dreamcast").FirstOrDefault(b => b.FileName == "dc_flash.bin");
            dcFlash.Should().NotBeNull();
            dcFlash.DownloadUrl.Should().Be("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/Sega/Dreamcast/dc_flash.bin");

            var ps2Bios = Subject.GetCatalog("ps2").FirstOrDefault(b => b.FileName == "scph39001.bin");
            ps2Bios.Should().NotBeNull();
            ps2Bios.DownloadUrl.Should().Be("https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/pcsx2/bios/ps2-0160a-20020207.bin");
        }

        [Test]
        public void Should_exhaust_all_viable_bios_candidates_across_timeouts_http_errors_and_hash_mismatches()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(a => a.AppDataFolder)
                  .Returns(_tempDir);

            // Real disksys.rom payload matching ExpectedMd5=ca30b50f880eb660a320674ed365ef7a, ExpectedSha1=57fe1bdee955bb48d357e463ccbf129496930b62
            // We'll use a custom BiosService or mock an Archive.org secondary MD5 query returning a valid zip or file.
            // Here we verify that timeouts (TimeoutException), 500 errors, and invalid hashes all continue to subsequent candidates
            // including Archive.org secondary MD5/baseName queries.
            var attemptedUrls = new List<string>();
            var callCount = 0;

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(req =>
                  {
                      var url = req.Url.ToString();
                      attemptedUrls.Add(url);
                      callCount++;

                      if (callCount == 1)
                      {
                          throw new TimeoutException("Simulated mirror timeout");
                      }

                      if (callCount == 2)
                      {
                          return new HttpResponse(req, new HttpHeader(), new byte[0], System.Net.HttpStatusCode.ServiceUnavailable);
                      }

                      if (url.Contains("archive.org/advancedsearch.php"))
                      {
                          if (url.Contains("ca30b50f880eb660a320674ed365ef7a"))
                          {
                              var md5SearchJson = "{\"response\":{\"docs\":[{\"identifier\":\"md5-pack\"}]}}";
                              return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(md5SearchJson));
                          }

                          var emptyJson = "{\"response\":{\"docs\":[]}}";
                          return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(emptyJson));
                      }

                      if (url.Contains("archive.org/metadata/md5-pack/files"))
                      {
                          var meta = "{\"result\":[{\"name\":\"disksys.rom\",\"md5\":\"ca30b50f880eb660a320674ed365ef7a\",\"sha1\":\"57fe1bdee955bb48d357e463ccbf129496930b62\"}]}";
                          return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes(meta));
                      }

                      return new HttpResponse(req, new HttpHeader(), Encoding.UTF8.GetBytes("bad_payload"));
                  });

            Subject.DownloadMissingBios("fds", "disksys.rom");

            attemptedUrls.Should().Contain(u => u.Contains("archive.org/advancedsearch.php") && u.Contains("disksys.rom"));
            attemptedUrls.Should().Contain(u => u.Contains("archive.org/advancedsearch.php") && u.Contains("ca30b50f880eb660a320674ed365ef7a"));
            attemptedUrls.Should().Contain(u => u.Contains("/download/md5-pack/disksys.rom"));
        }

        [Test]
        public void Should_not_verify_compressed_archive_directly_when_raw_bios_is_expected()
        {
            var rawPayload = Encoding.UTF8.GetBytes("raw_bios_payload_bytes");
            string expectedMd5;
            string expectedSha1;
            using (var md5 = MD5.Create())
            using (var sha1 = SHA1.Create())
            {
                expectedMd5 = BitConverter.ToString(md5.ComputeHash(rawPayload)).Replace("-", "").ToLowerInvariant();
                expectedSha1 = BitConverter.ToString(sha1.ComputeHash(rawPayload)).Replace("-", "").ToLowerInvariant();
            }

            var zipPath = Path.Combine(_tempDir, "test_bios.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("test_bios.bin");
                using var stream = entry.Open();
                stream.Write(rawPayload, 0, rawPayload.Length);
            }

            Subject.VerifyBios(zipPath, expectedMd5, expectedSha1).Should().BeFalse("VerifyBios must verify raw files, not compressed archives");

            var rawPath = Path.Combine(_tempDir, "test_bios.bin");
            File.WriteAllBytes(rawPath, rawPayload);
            Subject.VerifyBios(rawPath, expectedMd5, expectedSha1).Should().BeTrue();
        }

        [Test]
        public void Should_extract_and_verify_raw_bios_file_when_scanning_directory_with_zipped_bios()
        {
            var rawPayload = Encoding.UTF8.GetBytes("test_fds_raw_bios_payload");
            string rawMd5;
            string rawSha1;
            using (var md5 = MD5.Create())
            using (var sha1 = SHA1.Create())
            {
                rawMd5 = BitConverter.ToString(md5.ComputeHash(rawPayload)).Replace("-", "").ToLowerInvariant();
                rawSha1 = BitConverter.ToString(sha1.ComputeHash(rawPayload)).Replace("-", "").ToLowerInvariant();
            }

            var fdsEntry = BiosService.CanonicalBiosCatalog.First(b => b.GamePlatform == "fds" && b.FileName == "disksys.rom");
            var originalMd5 = fdsEntry.ExpectedMd5;
            var originalSha1 = fdsEntry.ExpectedSha1;

            try
            {
                fdsEntry.ExpectedMd5 = rawMd5;
                fdsEntry.ExpectedSha1 = rawSha1;

                var zipPath = Path.Combine(_tempDir, "disksys.zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("disksys.rom");
                    using var stream = entry.Open();
                    stream.Write(rawPayload, 0, rawPayload.Length);
                }

                var results = Subject.ScanAndVerifyBiosDirectory(_tempDir, "fds");
                var fdsBios = results.FirstOrDefault(b => b.FileName == "disksys.rom");
                var expectedRawPath = Path.Combine(_tempDir, "disksys.rom");

                File.Exists(expectedRawPath).Should().BeTrue("raw BIOS file should be extracted from the archive");
                fdsBios.Should().NotBeNull();
                fdsBios.IsVerified.Should().BeTrue();
                fdsBios.Status.Should().Be(1);
                fdsBios.ResolvedPath.Should().Be(expectedRawPath.Replace('\\', '/'));
            }
            finally
            {
                fdsEntry.ExpectedMd5 = originalMd5;
                fdsEntry.ExpectedSha1 = originalSha1;
            }
        }

        [Test]
        public void should_delete_platform_bios_directories_from_series_and_appdata()
        {
            var seriesPath = Path.Combine(_tempDir, "PlayStation");
            var seriesBiosDir = Path.Combine(seriesPath, "BIOS");
            var appDataDir = Path.Combine(_tempDir, "AppData");
            var appDataBiosDir = Path.Combine(appDataDir, "bios", "ps1");

            Directory.CreateDirectory(seriesBiosDir);
            Directory.CreateDirectory(appDataBiosDir);
            File.WriteAllBytes(Path.Combine(seriesBiosDir, "scph5501.bin"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(appDataBiosDir, "scph5501.bin"), new byte[] { 1, 2, 3 });

            Mocker.GetMock<IAppFolderInfo>()
                  .Setup(a => a.AppDataFolder)
                  .Returns(appDataDir);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series>());

            var series = new Series
            {
                Id = 42,
                Title = "PlayStation",
                TitleSlug = "ps1",
                Path = seriesPath
            };

            Subject.DeletePlatformBiosFiles(series);

            Directory.Exists(seriesBiosDir).Should().BeFalse();
            Directory.Exists(appDataBiosDir).Should().BeFalse();
        }

        [Test]
        public void Should_reject_bios_verification_when_expected_hashes_are_empty()
        {
            var payload = Encoding.UTF8.GetBytes("arbitrary_payload");
            var biosPath = Path.Combine(_tempDir, "unknown.bin");
            File.WriteAllBytes(biosPath, payload);

            Subject.VerifyBios(biosPath, null, null).Should().BeFalse();
            Subject.VerifyBios(biosPath, string.Empty, "   ").Should().BeFalse();
        }

        [Test]
        public void Should_exclude_arcade_and_computer_platforms_from_bios_catalog()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series>());

            var catalog = Subject.GetCatalog();
            var excludedGroups = new[] { "naomi", "msx", "amiga", "x68000", "pc98" };

            catalog.Select(g => g.GamePlatform).Should().NotContain(excludedGroups);
        }

        [Test]
        public void Should_include_dat_discovered_bios_firmware_and_boot_roms_in_bios_catalog()
        {
            var platformEntry = new PlatformCatalogEntry
            {
                TvdbId = 1012,
                Name = "Nintendo DS",
                Slug = "nds",
                LibretroSystemName = "Nintendo - Nintendo DS"
            };

            Mocker.GetMock<IGameCatalogService>()
                  .Setup(s => s.TryGetPlatformEntryBySlug("nds", out platformEntry))
                  .Returns(true);

            var datEntries = new List<DatEntry>
            {
                new DatEntry
                {
                    GameName = "[BIOS] Nintendo DS ARM7 (World)",
                    RomName = "bios7.bin",
                    Md5 = "df692a80a5b1bc90728bc3dfc76cd948",
                    Sha1 = "24f67bdea115a2c847c8813a262502ee1607b7df"
                },
                new DatEntry
                {
                    GameName = "Nintendo DS BootROM (World)",
                    RomName = "nds_dsi_boot.bin",
                    Md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    Sha1 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                },
                new DatEntry
                {
                    GameName = "Mario Kart DS (USA)",
                    RomName = "Mario Kart DS (USA).nds",
                    Md5 = "cccccccccccccccccccccccccccccccc",
                    Sha1 = "dddddddddddddddddddddddddddddddddddddddd"
                }
            };

            Mocker.GetMock<IDatCacheService>()
                  .Setup(d => d.GetEntriesForPlatform("nds", "Nintendo DS", "Nintendo - Nintendo DS"))
                  .Returns(datEntries);

            var catalog = Subject.GetCatalog("nds");

            catalog.Should().ContainSingle(b => b.FileName == "bios7.bin");
            var discoveredBoot = catalog.FirstOrDefault(b => b.FileName == "nds_dsi_boot.bin");
            discoveredBoot.Should().NotBeNull();
            discoveredBoot.IsRequired.Should().BeFalse();
            discoveredBoot.ExpectedMd5.Should().Be("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            discoveredBoot.ExpectedSha1.Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            catalog.Should().NotContain(b => b.FileName.Contains("Mario Kart DS"));
        }
    }
}
