using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DataAugmentation.Scene
{
    [TestFixture]
    public class SceneMappingProxyFixture : CoreTest<SceneMappingProxy>
    {
        [Test]
        public void fetch_should_return_empty_list()
        {
            var mappings = Subject.Fetch();

            mappings.Should().BeEmpty();
        }
    }
}
