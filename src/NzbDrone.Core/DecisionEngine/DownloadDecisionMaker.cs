using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly IEnumerable<IDownloadDecisionEngineSpecification> _specifications;
        private readonly IParsingService _parsingService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     IParsingService parsingService,
                                     ICustomFormatCalculationService formatService,
                                     IRemoteEpisodeAggregationService aggregationService,
                                     ISceneMappingService sceneMappingService,
                                     Logger logger)
        {
            _specifications = specifications;
            _parsingService = parsingService;
            _formatCalculator = formatService;
            _aggregationService = aggregationService;
            _sceneMappingService = sceneMappingService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetDecisions(reports, pushedRelease).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase)
        {
            return GetDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetDecisions(List<ReleaseInfo> reports, bool pushedRelease, SearchCriteriaBase searchCriteria = null)
        {
            if (reports.Any())
            {
                _logger.ProgressInfo("Processing {0} releases", reports.Count);
            }
            else
            {
                _logger.ProgressInfo("No releases found");
            }

            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                try
                {
                    var parsedEpisodeInfo = Parser.Parser.ParseTitle(report.Title);

                    if (parsedEpisodeInfo == null || parsedEpisodeInfo.IsPossibleSpecialEpisode)
                    {
                        var specialEpisodeInfo = _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, report.Title, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);

                        if (specialEpisodeInfo != null)
                        {
                            parsedEpisodeInfo = specialEpisodeInfo;
                        }
                    }

                    if (parsedEpisodeInfo == null)
                    {
                        // Attempt to parse as a release that includes the season title without a season number
                        parsedEpisodeInfo = Parser.Parser.ParseSeasonTitle(report.Title);
                    }

                    if (parsedEpisodeInfo == null || parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                    {
                        parsedEpisodeInfo = TryParseRomRelease(report.Title, searchCriteria);
                    }

                    if (parsedEpisodeInfo != null && !parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                    {
                        var remoteEpisode = _parsingService.Map(parsedEpisodeInfo, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);
                        remoteEpisode.Release = report;
                        remoteEpisode.ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria);
                        remoteEpisode.ParsedRomInfo = report.ParsedRomInfo;

                        if (remoteEpisode.Series == null)
                        {
                            var matchingTvdbId = _sceneMappingService.FindTvdbId(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber ?? -1);

                            if (matchingTvdbId.HasValue)
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.MatchesAnotherSeries, $"{parsedEpisodeInfo.SeriesTitle} matches an alias for series with TVDB ID: {matchingTvdbId}"));
                            }
                            else
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownSeries, "Unknown Platform"));
                            }
                        }
                        else if (remoteEpisode.Episodes.Empty())
                        {
                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownEpisode, "Unable to identify correct episode(s) using release name and scene mappings"));
                        }
                        else
                        {
                            _aggregationService.Augment(remoteEpisode);

                            remoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(remoteEpisode, remoteEpisode.Release.Size);
                            remoteEpisode.CustomFormatScore = remoteEpisode?.Series?.QualityProfile?.Value.CalculateCustomFormatScore(remoteEpisode.CustomFormats) ?? 0;

                            _logger.Trace("Custom Format Score of '{0}' [{1}] calculated for '{2}'", remoteEpisode.CustomFormatScore, remoteEpisode.CustomFormats?.ConcatToString(), report.Title);

                            remoteEpisode.DownloadAllowed = remoteEpisode.Episodes.Any();
                            decision = GetDecisionForReport(remoteEpisode, new ReleaseDecisionInformation(pushedRelease, searchCriteria));
                        }
                    }

                    if (searchCriteria != null)
                    {
                        if (parsedEpisodeInfo == null)
                        {
                            parsedEpisodeInfo = new ParsedEpisodeInfo
                            {
                                Languages = LanguageParser.ParseLanguages(report.Title),
                                Quality = QualityParser.ParseQuality(report.Title)
                            };
                        }

                        if (parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                        {
                            var remoteEpisode = new RemoteEpisode
                            {
                                Release = report,
                                ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria),
                                ParsedEpisodeInfo = parsedEpisodeInfo,
                                Languages = parsedEpisodeInfo.Languages,
                            };

                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnableToParse, "Unable to parse release"));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var remoteEpisode = new RemoteEpisode { Release = report, ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria) };

                    decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.Error, "Unexpected error processing release"));
                }

                reportNumber++;

                if (decision != null)
                {
                    if (decision.Rejections.Any())
                    {
                        _logger.Debug("Release '{0}' from '{1}' rejected for the following reasons: {2}", report.Title, report.Indexer, string.Join(", ", decision.Rejections));
                    }
                    else
                    {
                        _logger.Debug("Release '{0}' from '{1}' accepted", report.Title, report.Indexer);
                    }

                    yield return decision;
                }
            }
        }

        private static ParsedEpisodeInfo TryParseRomRelease(string releaseTitle, SearchCriteriaBase searchCriteria)
        {
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                return null;
            }

            var romInfo = RomParser.ParseRomName(releaseTitle);
            var cleanRomGameTitle = CleanRomTitleForComparison(romInfo?.GameTitle ?? releaseTitle);

            if (searchCriteria != null)
            {
                var series = searchCriteria.Series;
                if (series != null)
                {
                    // Check if this is a single game search and the release title matches the searched episode
                    if (searchCriteria.Episodes != null && searchCriteria.Episodes.Any())
                    {
                        var matchingEp = searchCriteria.Episodes.FirstOrDefault(ep =>
                            IsRomTitleMatch(cleanRomGameTitle, ep.Title));

                        if (matchingEp != null)
                        {
                            var romQuality = romInfo?.Quality ?? new QualityModel(Quality.VerifiedGoodDump);
                            if (romQuality.Quality == Quality.Unknown)
                            {
                                romQuality = new QualityModel(Quality.VerifiedGoodDump);
                            }

                            return new ParsedEpisodeInfo
                            {
                                SeriesTitle = series.Title,
                                SeriesTitleInfo = new SeriesTitleInfo { Title = series.Title },
                                SeasonNumber = matchingEp.SeasonNumber,
                                SeasonNumbers = new List<int> { matchingEp.SeasonNumber },
                                EpisodeNumbers = new[] { matchingEp.EpisodeNumber },
                                AbsoluteEpisodeNumbers = new[] { matchingEp.AbsoluteEpisodeNumber ?? matchingEp.EpisodeNumber },
                                ReleaseTitle = releaseTitle,
                                Languages = LanguageParser.ParseLanguages(releaseTitle),
                                Quality = romQuality
                            };
                        }
                    }

                    // Check if this is a platform full set or season pack matching the platform
                    if (IsPlatformPackMatch(releaseTitle, cleanRomGameTitle, series))
                    {
                        var targetSeason = searchCriteria is SeasonSearchCriteria ssc ? ssc.SeasonNumber : 1;
                        var romQuality = romInfo?.Quality ?? new QualityModel(Quality.VerifiedGoodDump);
                        if (romQuality.Quality == Quality.Unknown)
                        {
                            romQuality = new QualityModel(Quality.VerifiedGoodDump);
                        }

                        return new ParsedEpisodeInfo
                        {
                            SeriesTitle = series.Title,
                            SeriesTitleInfo = new SeriesTitleInfo { Title = series.Title },
                            FullSeason = true,
                            SeasonNumber = targetSeason,
                            SeasonNumbers = new List<int> { targetSeason },
                            ReleaseTitle = releaseTitle,
                            Languages = LanguageParser.ParseLanguages(releaseTitle),
                            Quality = romQuality
                        };
                    }
                }
            }

            return null;
        }

        private static bool IsPlatformPackMatch(string releaseTitle, string cleanRomGameTitle, NzbDrone.Core.Tv.Series series)
        {
            if (series == null || string.IsNullOrWhiteSpace(releaseTitle))
            {
                return false;
            }

            var cleanSeriesTitle = CleanRomTitleForComparison(series.Title);
            if (IsRomTitleMatch(cleanRomGameTitle, cleanSeriesTitle) ||
                releaseTitle.Contains(series.Title, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(series.TitleSlug) && releaseTitle.Contains(series.TitleSlug, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var isArchive = releaseTitle.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("SMDB", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("HTGDB", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("Full Set", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("1G1R", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("No-Intro", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("Redump", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("TOSEC", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("Romset", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.Contains("Complete", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.StartsWith("@", StringComparison.OrdinalIgnoreCase) ||
                            releaseTitle.StartsWith("[", StringComparison.OrdinalIgnoreCase);

            if (!isArchive)
            {
                return false;
            }

            var slug = series.TitleSlug?.ToLowerInvariant() ?? string.Empty;
            var releaseLower = releaseTitle.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(slug) && slug.Length >= 2 && releaseLower.Contains(slug))
            {
                return true;
            }

            var aliases = slug switch
            {
                "snes" => new[] { "snes", "sd2snes", "super nintendo", "super famicom", "sfc", "super everdrive" },
                "nes" => new[] { "nes", "everdrive n8", "famicom", "nintendo entertainment system" },
                "genesis" or "megadrive" => new[] { "genesis", "mega everdrive", "mega drive", "megadrive", "sega genesis", "smd" },
                "n64" => new[] { "n64", "everdrive 64", "nintendo 64" },
                "gb" => new[] { "game boy", "gameboy", "everdrive gb" },
                "gbc" => new[] { "game boy color", "gameboy color", "everdrive gb", "gbc" },
                "gba" => new[] { "game boy advance", "gameboy advance", "everdrive gba", "ez-flash", "gba" },
                "3ds" => new[] { "3ds", "nintendo 3ds" },
                "nds" or "ds" => new[] { "nds", "nintendo ds" },
                "ps1" or "psx" => new[] { "ps1", "psx", "playstation" },
                "ps2" => new[] { "ps2", "playstation 2" },
                "psp" => new[] { "psp", "playstation portable" },
                "saturn" => new[] { "saturn", "sega saturn" },
                "dreamcast" or "dc" => new[] { "dreamcast", "sega dreamcast" },
                "segacd" => new[] { "segacd", "sega cd", "mega-cd", "megacd" },
                "32x" => new[] { "32x", "sega 32x" },
                "sms" => new[] { "master everdrive", "sega master system", "sms" },
                "gg" => new[] { "game gear", "gamegear", "everdrive gg" },
                "tg16" => new[] { "turbo everdrive", "pc engine", "turbografx", "pce" },
                "tgcd" => new[] { "tgcd", "turbografx-cd", "pc engine cd" },
                "atari2600" => new[] { "harmony", "atari 2600" },
                "neogeo" => new[] { "neosd", "neo geo", "neogeo" },
                _ => Array.Empty<string>()
            };

            return aliases.Any(a => releaseLower.Contains(a));
        }

        private static string CleanRomTitleForComparison(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var clean = RomParser.CleanIndexPrefix(title.Trim());
            var parenIdx = clean.IndexOf('(');
            if (parenIdx > 0)
            {
                clean = clean.Substring(0, parenIdx);
            }

            var bracketIdx = clean.IndexOf('[');
            if (bracketIdx > 0)
            {
                clean = clean.Substring(0, bracketIdx);
            }

            clean = clean.Trim();
            if (clean.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(4).Trim();
            }
            else if (clean.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(2).Trim();
            }
            else if (clean.StartsWith("An ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(3).Trim();
            }

            var commaTheIdx = clean.LastIndexOf(", The", StringComparison.OrdinalIgnoreCase);
            if (commaTheIdx >= 0)
            {
                clean = clean.Substring(0, commaTheIdx).Trim();
            }

            var sb = new StringBuilder(clean.Length);
            foreach (var ch in clean)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        private static bool IsRomTitleMatch(string cleanCandidate, string rawTarget)
        {
            var cleanTarget = CleanRomTitleForComparison(rawTarget);
            if (string.IsNullOrWhiteSpace(cleanCandidate) || string.IsNullOrWhiteSpace(cleanTarget))
            {
                return false;
            }

            return cleanCandidate.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                   cleanCandidate.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                   cleanTarget.StartsWith(cleanCandidate, StringComparison.OrdinalIgnoreCase);
        }

        private DownloadDecision GetDecisionForReport(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            var reasons = Array.Empty<DownloadRejection>();

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteEpisode, information))
                                        .Where(c => c != null)
                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteEpisode, reasons.ToArray());
        }

        private DownloadRejection EvaluateSpec(IDownloadDecisionEngineSpecification spec, RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            try
            {
                var result = spec.IsSatisfiedBy(remoteEpisode, information);

                if (!result.Accepted)
                {
                    return new DownloadRejection(result.Reason, result.Message, spec.Type);
                }
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteEpisode.Release.ToJson());
                e.Data.Add("parsed", remoteEpisode.ParsedEpisodeInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}", remoteEpisode.Release.Title);
                return new DownloadRejection(DownloadRejectionReason.DecisionError, $"{spec.GetType().Name}: {e.Message}");
            }

            return null;
        }

        private ReleaseSourceType GetReleaseSource(bool pushedRelease, SearchCriteriaBase searchCriteria = null)
        {
            if (searchCriteria == null)
            {
                return pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;
            }

            if (searchCriteria.InteractiveSearch)
            {
                return ReleaseSourceType.InteractiveSearch;
            }
            else if (searchCriteria.UserInvokedSearch)
            {
                return ReleaseSourceType.UserInvokedSearch;
            }
            else
            {
                return ReleaseSourceType.Search;
            }
        }
    }
}
