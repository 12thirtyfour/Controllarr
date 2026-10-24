using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class VerifiedRomHashSpecificationFixture : CoreTest<VerifiedRomHashSpecification>
    {
        [Test]
        public void should_accept_when_rom_hash_is_verified()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Super Mario World (USA).sfc",
                RomHashVerified = true,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "b19ed489" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-1",
                Title = "Super Mario World (USA)"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_hold_in_activity_queue_when_automated_download_hash_is_unverified()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Unknown Dump (USA).sfc",
                RomHashVerified = false,
                RomHashes = new Dictionary<string, string>
                {
                    { "CRC32", "deadbeef" }
                }
            };

            var downloadItem = new DownloadClientItem
            {
                DownloadId = "dl-2",
                Title = "Unknown Dump (USA)"
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, downloadItem);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.UnverifiedRomHash);
        }

        [Test]
        public void should_accept_manual_import_when_download_client_item_is_null()
        {
            var localEpisode = new LocalEpisode
            {
                Path = @"C:\ManualImport\Custom Translation.sfc",
                RomHashVerified = false
            };

            var decision = Subject.IsSatisfiedBy(localEpisode, null);
            decision.Accepted.Should().BeTrue();
        }
    }
}
