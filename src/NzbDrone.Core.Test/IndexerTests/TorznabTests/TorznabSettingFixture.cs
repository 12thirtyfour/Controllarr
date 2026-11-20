using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.TorznabTests
{
    public class TorznabSettingFixture : CoreTest
    {
        [Test]
        public void default_categories_should_only_contain_console_categories()
        {
            var setting = new TorznabSettings();

            setting.Categories.Should().NotBeEmpty();
            setting.Categories.Should().OnlyContain(c => c >= 1000 && c < 2000);
            setting.Categories.Should().NotContain(c => c >= 4000 && c < 5000);
            setting.Categories.Should().NotContain(c => c >= 8000 && c < 9000);
        }
    }
}
