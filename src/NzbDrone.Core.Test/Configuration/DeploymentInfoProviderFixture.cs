using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class DeploymentInfoProviderFixture : TestBase
    {
        [Test]
        public void should_parse_docker_package_info_when_running_from_bin_folder()
        {
            var binFolder = Path.Combine(TempFolder, "app", "bin");
            var packageInfoPath = Path.GetFullPath(Path.Combine(binFolder, "..", "package_info"));

            var appFolderMock = new Mock<IAppFolderInfo>();
            appFolderMock.SetupGet(x => x.StartUpFolder).Returns(binFolder);

            var diskMock = new Mock<IDiskProvider>();
            diskMock.Setup(x => x.FileExists(packageInfoPath)).Returns(true);
            diskMock.Setup(x => x.ReadAllText(packageInfoPath)).Returns(
                "PackageVersion=5.0.0.160-develop\n" +
                "PackageAuthor=Team Controllarr (https://github.com/12thirtyfour/Controllarr)\n" +
                "UpdateMethod=Docker\n" +
                "UpdateMethodMessage=Pull the latest Controllarr Docker image to update\n" +
                "Branch=develop\n" +
                "ReleaseVersion=5.0.0.160\n");

            var provider = new DeploymentInfoProvider(appFolderMock.Object, diskMock.Object);

            provider.PackageUpdateMechanism.Should().Be(UpdateMechanism.Docker);
            provider.DefaultUpdateMechanism.Should().Be(UpdateMechanism.Docker);
            provider.IsExternalUpdateMechanism.Should().BeTrue();
            provider.PackageVersion.Should().Be("5.0.0.160-develop");
            provider.PackageAuthor.Should().Be("Team Controllarr (https://github.com/12thirtyfour/Controllarr)");
            provider.PackageBranch.Should().Be("develop");
            provider.DefaultBranch.Should().Be("develop");
            provider.ReleaseVersion.Should().Be("5.0.0.160");
        }

        [Test]
        public void should_default_to_builtin_updater_when_no_package_info_exists()
        {
            var binFolder = Path.Combine(TempFolder, "app", "bin");
            var appFolderMock = new Mock<IAppFolderInfo>();
            appFolderMock.SetupGet(x => x.StartUpFolder).Returns(binFolder);

            var diskMock = new Mock<IDiskProvider>();
            diskMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

            var provider = new DeploymentInfoProvider(appFolderMock.Object, diskMock.Object);

            provider.PackageUpdateMechanism.Should().Be(UpdateMechanism.BuiltIn);
            provider.IsExternalUpdateMechanism.Should().BeFalse();
        }
    }
}
