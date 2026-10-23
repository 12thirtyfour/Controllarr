using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games.RetroAchievements;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class RetroAchievementsServiceFixture
    {
        private RetroAchievementsService _subject;

        [SetUp]
        public void Setup()
        {
            _subject = new RetroAchievementsService();
        }

        [TestCase("RetroPlayer", "AbCdEfGh1234567890ZyXwVu", true)]
        [TestCase("", "AbCdEfGh1234567890ZyXwVu", false)]
        [TestCase("RetroPlayer", "shortkey", false)]
        public void should_validate_retroachievements_credentials(string username, string apiKey, bool expected)
        {
            _subject.ValidateCredentials(username, apiKey).Should().Be(expected);
        }

        [Test]
        public void should_calculate_mastery_progress_and_badge_showcase()
        {
            var badges = new List<RetroAchievementBadge>
            {
                new RetroAchievementBadge { Id = 1, Title = "First Boss", Points = 10, Unlocked = true, HardcoreUnlocked = true },
                new RetroAchievementBadge { Id = 2, Title = "All Secrets", Points = 25, Unlocked = true, HardcoreUnlocked = true }
            };

            var progress = _subject.CalculateMasteryProgress("RetroPlayer", "Super Metroid", "ABCD1234EF567890", badges);

            progress.TotalAchievements.Should().Be(2);
            progress.UnlockedAchievements.Should().Be(2);
            progress.HardcoreUnlockedAchievements.Should().Be(2);
            progress.MasteryPercentage.Should().Be(100m);
            progress.IsMastered.Should().BeTrue();
            progress.RomMd5Hash.Should().Be("abcd1234ef567890");
        }
    }
}
