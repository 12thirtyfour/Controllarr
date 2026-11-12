using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.RootFolderTests
{
    [TestFixture]
    public class RootFolderRepositoryFixture : DbTest<RootFolderRepository, RootFolder>
    {
        [Test]
        public void should_insert_and_retrieve_root_folder_ignoring_computed_properties()
        {
            var rootFolder = new RootFolder
            {
                Path = @"C:\Games".AsOsAgnostic(),
                Accessible = true,
                IsEmpty = false,
                FreeSpace = 1024,
                TotalSpace = 2048,
                ContainsRomFiles = true,
                UnmappedFolders = new List<UnmappedFolder>()
            };

            var inserted = Subject.Insert(rootFolder);

            inserted.Id.Should().BeGreaterThan(0);
            inserted.Path.Should().Be(rootFolder.Path);

            var all = Subject.All();
            all.Should().ContainSingle(r => r.Id == inserted.Id && r.Path == rootFolder.Path);
        }
    }
}
