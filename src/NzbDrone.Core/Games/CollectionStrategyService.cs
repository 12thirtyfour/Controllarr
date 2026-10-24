using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Games
{
    public interface ICollectionStrategyService
    {
        CollectionStrategy ResolveEffectiveStrategy(GamePlatform platform, CollectionStrategy globalDefault = CollectionStrategy.OneGameOneRom);
        List<ParsedRomInfo> FilterCandidates(IEnumerable<ParsedRomInfo> candidates, CollectionStrategy strategy, RevisionPreference revisionPreference, string pinnedVersion = null, IReadOnlyList<string> preferredRegions = null);
        int GetRegionRank(ParsedRomInfo romInfo, IReadOnlyList<string> preferredRegions = null);
        bool IsPreReleaseCandidate(ParsedRomInfo romInfo);
    }

    public class CollectionStrategyService : ICollectionStrategyService
    {
        private static readonly string[] DefaultPreferredRegions = { "USA", "World", "Europe", "Japan" };
        private static readonly string[] PreReleaseTags = { "Beta", "Proto", "Prototype", "Demo", "Sample", "Promo", "Preview", "Kiosk" };
        private static readonly Regex PreReleaseRegex = new Regex(@"\b(Proto|Prototype|Beta|Demo|Sample|Promo|Preview|Kiosk)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public CollectionStrategy ResolveEffectiveStrategy(GamePlatform platform, CollectionStrategy globalDefault = CollectionStrategy.OneGameOneRom)
        {
            if (platform == null)
            {
                return globalDefault;
            }

            return platform.CollectionStrategy;
        }

        public bool IsPreReleaseCandidate(ParsedRomInfo romInfo)
        {
            if (romInfo == null)
            {
                return false;
            }

            if (romInfo.DumpTags != null && romInfo.DumpTags.Any(t => PreReleaseTags.Any(p => t.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(romInfo.Revision) && PreReleaseRegex.IsMatch(romInfo.Revision))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(romInfo.Version) && PreReleaseRegex.IsMatch(romInfo.Version))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(romInfo.ReleaseTitle) && PreReleaseRegex.IsMatch(romInfo.ReleaseTitle))
            {
                return true;
            }

            return false;
        }

        public List<ParsedRomInfo> FilterCandidates(
            IEnumerable<ParsedRomInfo> candidates,
            CollectionStrategy strategy,
            RevisionPreference revisionPreference,
            string pinnedVersion = null,
            IReadOnlyList<string> preferredRegions = null)
        {
            if (candidates == null)
            {
                return new List<ParsedRomInfo>();
            }

            var candidateList = candidates.Where(c => c != null).ToList();
            if (strategy == CollectionStrategy.Preservationist)
            {
                return candidateList;
            }

            // Demos, Protos, Betas, and Samples are never considered for 1G1R
            candidateList = candidateList.Where(c => !IsPreReleaseCandidate(c)).ToList();
            if (candidateList.Count == 0)
            {
                return candidateList;
            }

            var regions = preferredRegions != null && preferredRegions.Count > 0 ? preferredRegions : DefaultPreferredRegions;

            if (strategy == CollectionStrategy.OneGameOneRom)
            {
                var winner = SelectWinner(candidateList, revisionPreference, pinnedVersion, regions);
                return winner != null ? new List<ParsedRomInfo> { winner } : new List<ParsedRomInfo>();
            }

            if (strategy == CollectionStrategy.OneGamePerRegionPerConsole)
            {
                var results = new List<ParsedRomInfo>();
                var groupedByRegion = candidateList.GroupBy(
                    c => c.Regions != null && c.Regions.Count > 0 ? c.Regions[0].ToUpperInvariant() : "UNKNOWN",
                    StringComparer.OrdinalIgnoreCase);

                foreach (var regionGroup in groupedByRegion)
                {
                    var regionalWinner = SelectWinner(regionGroup.ToList(), revisionPreference, pinnedVersion, regions);
                    if (regionalWinner != null)
                    {
                        results.Add(regionalWinner);
                    }
                }

                return results;
            }

            return candidateList;
        }

        public int GetRegionRank(ParsedRomInfo romInfo, IReadOnlyList<string> preferredRegions = null)
        {
            if (romInfo?.Regions == null || romInfo.Regions.Count == 0)
            {
                return 0;
            }

            var regions = preferredRegions != null && preferredRegions.Count > 0 ? preferredRegions : DefaultPreferredRegions;
            var bestIndex = int.MaxValue;

            foreach (var romRegion in romInfo.Regions)
            {
                for (var i = 0; i < regions.Count; i++)
                {
                    if (string.Equals(romRegion, regions[i], StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < bestIndex)
                        {
                            bestIndex = i;
                        }
                    }
                }
            }

            return bestIndex == int.MaxValue ? 1 : regions.Count - bestIndex + 1;
        }

        private ParsedRomInfo SelectWinner(
            List<ParsedRomInfo> group,
            RevisionPreference revisionPreference,
            string pinnedVersion,
            IReadOnlyList<string> preferredRegions)
        {
            if (group == null || group.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(pinnedVersion))
            {
                var pinned = group.FirstOrDefault(c =>
                    string.Equals(c.Revision, pinnedVersion, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Version, pinnedVersion, StringComparison.OrdinalIgnoreCase));

                if (pinned != null)
                {
                    return pinned;
                }
            }

            return group
                .OrderByDescending(c => GetRegionRank(c, preferredRegions))
                .ThenByDescending(c => GetRevisionScore(c, revisionPreference))
                .FirstOrDefault();
        }

        private static string GetRevisionScore(ParsedRomInfo romInfo, RevisionPreference preference)
        {
            var rev = romInfo?.Revision ?? romInfo?.Version ?? string.Empty;
            if (preference == RevisionPreference.LatestRevision)
            {
                return string.IsNullOrWhiteSpace(rev) ? "0000" : $"1_{rev.ToUpperInvariant()}";
            }

            return string.IsNullOrWhiteSpace(rev) ? "ZZZZ" : $"0_{rev.ToUpperInvariant()}";
        }
    }
}
