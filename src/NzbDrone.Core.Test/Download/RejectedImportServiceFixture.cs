using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class RejectedImportServiceFixture : CoreTest<RejectedImportService>
    {
        [Test]
        public void should_fail_without_blocklisting_when_rejection_is_duplicate_library_hash()
        {
            var trackedDownload = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem
                {
                    Title = "Duplicate.Game.ROM",
                    DownloadId = "dl-123"
                },
                RemoteEpisode = new RemoteEpisode
                {
                    Release = new ReleaseInfo
                    {
                        IndexerId = 1,
                        Title = "Duplicate.Game.ROM"
                    }
                }
            };

            var importDecision = new ImportDecision(
                new LocalEpisode { Path = @"C:\Downloads\Duplicate.Game.nes" },
                new ImportRejection(ImportRejectionReason.DuplicateLibraryHash, "Matches already imported library file"));

            var importResult = new ImportResult(importDecision, "Matches already imported library file");

            var handled = Subject.Process(trackedDownload, importResult);

            handled.Should().BeTrue();
            trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
            trackedDownload.SkipBlocklist.Should().BeTrue();
            trackedDownload.DownloadItem.CanBeRemoved.Should().BeTrue();
        }
    }
}
