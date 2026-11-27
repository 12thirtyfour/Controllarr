using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityFixture : CoreTest
    {
        public static object[] FromIntCases =
        {
            new object[] { 0, Quality.Unknown },
            new object[] { 100, Quality.VerifiedGoodDump },
            new object[] { 101, Quality.VerifiedRedump },
            new object[] { 102, Quality.NoIntroVerified },
            new object[] { 103, Quality.UnverifiedRaw },
            new object[] { 104, Quality.BetaProto },
            new object[] { 105, Quality.TranslationPatch },
            new object[] { 1, Quality.Unknown }
        };

        public static object[] ToIntCases =
        {
            new object[] { Quality.Unknown, 0 },
            new object[] { Quality.VerifiedGoodDump, 100 },
            new object[] { Quality.VerifiedRedump, 101 },
            new object[] { Quality.NoIntroVerified, 102 },
            new object[] { Quality.UnverifiedRaw, 103 },
            new object[] { Quality.BetaProto, 104 },
            new object[] { Quality.TranslationPatch, 105 }
        };

        [Test]
        [TestCaseSource(nameof(FromIntCases))]
        public void should_be_able_to_convert_int_to_qualityTypes(int source, Quality expected)
        {
            var quality = (Quality)source;
            quality.Should().Be(expected);
        }

        [Test]
        [TestCaseSource(nameof(ToIntCases))]
        public void should_be_able_to_convert_qualityTypes_to_int(Quality source, int expected)
        {
            var i = (int)source;
            i.Should().Be(expected);
        }

        public static List<QualityProfileQualityItem> GetDefaultQualities(params Quality[] allowed)
        {
            var qualities = new List<Quality>
            {
                Quality.Unknown,
                Quality.BetaProto,
                Quality.TranslationPatch,
                Quality.UnverifiedRaw,
                Quality.VerifiedGoodDump,
                Quality.NoIntroVerified,
                Quality.VerifiedRedump
            };

            if (allowed.Length == 0)
            {
                allowed = qualities.ToArray();
            }

            var items = qualities
                .Except(allowed)
                .Concat(allowed)
                .Select(v => new QualityProfileQualityItem { Quality = v, Allowed = allowed.Contains(v) }).ToList();

            return items;
        }
    }
}
