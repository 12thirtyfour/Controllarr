using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Games.RetroAchievements
{
    public class RetroAchievementBadge
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Points { get; set; }
        public string BadgeName { get; set; }
        public bool Unlocked { get; set; }
        public bool HardcoreUnlocked { get; set; }
    }

    public class RetroAchievementsGameProgress
    {
        public string Username { get; set; }
        public string GameTitle { get; set; }
        public string RomMd5Hash { get; set; }
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public int HardcoreUnlockedAchievements { get; set; }
        public decimal MasteryPercentage { get; set; }
        public bool IsMastered { get; set; }
        public List<RetroAchievementBadge> Badges { get; set; } = new List<RetroAchievementBadge>();
    }

    public interface IRetroAchievementsService
    {
        bool ValidateCredentials(string username, string webApiKey);
        RetroAchievementsGameProgress CalculateMasteryProgress(
            string username,
            string gameTitle,
            string romMd5Hash,
            IReadOnlyCollection<RetroAchievementBadge> badges);
    }

    public class RetroAchievementsService : IRetroAchievementsService
    {
        private static readonly Regex ApiKeyRegex = new Regex("^[A-Za-z0-9]{16,64}$", RegexOptions.Compiled);

        public bool ValidateCredentials(string username, string webApiKey)
        {
            if (username.IsNullOrWhiteSpace() || webApiKey.IsNullOrWhiteSpace())
            {
                return false;
            }

            var trimmedUser = username.Trim();
            var trimmedKey = webApiKey.Trim();

            return trimmedUser.Length >= 2 && trimmedUser.Length <= 32 && ApiKeyRegex.IsMatch(trimmedKey);
        }

        public RetroAchievementsGameProgress CalculateMasteryProgress(
            string username,
            string gameTitle,
            string romMd5Hash,
            IReadOnlyCollection<RetroAchievementBadge> badges)
        {
            var badgeList = badges != null ? new List<RetroAchievementBadge>(badges) : new List<RetroAchievementBadge>();
            var total = badgeList.Count;
            var unlocked = 0;
            var hardcore = 0;

            foreach (var badge in badgeList)
            {
                if (badge == null)
                {
                    continue;
                }

                if (badge.Unlocked || badge.HardcoreUnlocked)
                {
                    unlocked++;
                }

                if (badge.HardcoreUnlocked)
                {
                    hardcore++;
                }
            }

            var percentage = total > 0
                ? Math.Round((decimal)unlocked / total * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            return new RetroAchievementsGameProgress
            {
                Username = username?.Trim() ?? string.Empty,
                GameTitle = gameTitle?.Trim() ?? string.Empty,
                RomMd5Hash = romMd5Hash?.Trim().ToLowerInvariant() ?? string.Empty,
                TotalAchievements = total,
                UnlockedAchievements = unlocked,
                HardcoreUnlockedAchievements = hardcore,
                MasteryPercentage = percentage,
                IsMastered = total > 0 && hardcore == total,
                Badges = badgeList
            };
        }
    }
}
