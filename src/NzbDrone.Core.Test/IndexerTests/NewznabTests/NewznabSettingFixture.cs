using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    public class NewznabSettingFixture : CoreTest
    {
        [TestCase("http://nzbs.org")]
        [TestCase("http:///www.nzbplanet.net")]
        public void requires_apikey(string url)
        {
            var setting = new NewznabSettings()
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeFalse();
            setting.Validate().Errors.Should().Contain(c => c.PropertyName == nameof(NewznabSettings.ApiKey));
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase(null)]
        public void invalid_url_should_not_apikey(string url)
        {
            var setting = new NewznabSettings
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeFalse();
            setting.Validate().Errors.Should().NotContain(c => c.PropertyName == nameof(NewznabSettings.ApiKey));
            setting.Validate().Errors.Should().Contain(c => c.PropertyName == nameof(NewznabSettings.BaseUrl));
        }

        [TestCase("http://nzbs2.org")]
        public void doesnt_requires_apikey(string url)
        {
            var setting = new NewznabSettings()
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeTrue();
        }

        [Test]
        public void default_categories_should_only_contain_console_categories()
        {
            var setting = new NewznabSettings();

            setting.Categories.Should().NotBeEmpty();
            setting.Categories.Should().OnlyContain(c => c >= 1000 && c < 2000);
            setting.Categories.Should().NotContain(c => c >= 4000 && c < 5000);
            setting.Categories.Should().NotContain(c => c >= 8000 && c < 9000);
        }
    }
}
