using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class SceneCheckerFixture
    {
        [TestCase("Super.Mario.64.S04E13.USA.[No-Intro]-Mercury")]
        [TestCase("Final.Fantasy.VII.S07E02.[Redump]-Mercury")]
        [TestCase("Chrono.Trigger.2009.S05E06.Rev.1.[!]-Mercury")]
        [TestCase("The.Legend.of.Zelda.S04E17.[No-Intro]-Mercury")]
        [TestCase("Metroid.Prime.S04.[Redump]-Mercury")]
        public void should_return_true_for_scene_names(string title)
        {
            SceneChecker.IsSceneTitle(title).Should().BeTrue();
        }

        [TestCase("S08E05 - Super Mario 64 [No-Intro]")]
        [TestCase("S08E05 - Super.Mario.64 [No-Intro]")]
        [TestCase("Something")]
        [TestCase("86de66b7ef385e2fa56a3e41b98481ea1658bfab")]
        [TestCase("Super.Mario.64.S04E17.[Redump]", Description = "no group")]
        [TestCase("S04E17.[No-Intro]-Mercury", Description = "no game title")]
        [TestCase("Super.Mario.64.S04E17-Mercury", Description = "no quality")]
        [TestCase("Super.Mario.64.[No-Intro]-Mercury", Description = "no game number")]
        public void should_return_false_for_non_scene_names(string title)
        {
            SceneChecker.IsSceneTitle(title).Should().BeFalse();
        }
    }
}
