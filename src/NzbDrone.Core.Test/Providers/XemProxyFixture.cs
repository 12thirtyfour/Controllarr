using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Xem;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Providers
{
    [TestFixture]
    public class XemProxyFixture : CoreTest<XemProxy>
    {
        [Test]
        public void get_series_ids_should_return_empty()
        {
            Subject.GetXemSeriesIds().Should().BeEmpty();
        }

        [Test]
        public void get_scene_tvdb_mappings_should_return_empty()
        {
            Subject.GetSceneTvdbMappings(12345).Should().BeEmpty();
        }

        [Test]
        public void get_scene_tvdb_names_should_return_empty()
        {
            Subject.GetSceneTvdbNames().Should().BeEmpty();
        }
    }
}
