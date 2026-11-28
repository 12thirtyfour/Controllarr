using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.Games;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly IDownloadDecisionEngineSpecification[][] _orderedSpecificationGroups;
        private readonly IParsingService _parsingService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IRemoteGameAggregationService _aggregationService;
        private readonly IEpisodeService _episodeService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     IParsingService parsingService,
                                     ICustomFormatCalculationService formatService,
                                     IRemoteGameAggregationService aggregationService,
                                     Logger logger,
                                     IEpisodeService episodeService = null)
        {
            _orderedSpecificationGroups = specifications
                .GroupBy(v => v.Priority)
                .OrderBy(v => v.Key)
                .Select(g => g.ToArray())
                .ToArray();
            _parsingService = parsingService;
            _formatCalculator = formatService;
            _aggregationService = aggregationService;
            _episodeService = episodeService;
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

            var episodeLookup = BuildSearchEpisodeLookup(searchCriteria);
            Dictionary<int, SearchEpisodeTitleLookup> rssEpisodeLookups = null;
            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                ParsedEpisodeInfo parsedEpisodeInfo = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                try
                {
                    if (PlatformCompatibilityValidator.IsTvOrVideoRelease(report.Title))
                    {
                        _logger.Debug("Skipping release '{0}' because it is a TV or video release", report.Title);
                        reportNumber++;
                        continue;
                    }

                    if (PlatformCompatibilityValidator.IsPcOrNonConsoleRelease(report.Title))
                    {
                        _logger.Debug("Skipping release '{0}' because it is a PC or non-console release", report.Title);
                        reportNumber++;
                        continue;
                    }

                    if (PlatformCompatibilityValidator.IsNonRomContentRelease(report.Title))
                    {
                        _logger.Debug("Skipping release '{0}' because it is non-ROM content (save file, guide, cheats, or artwork)", report.Title);
                        reportNumber++;
                        continue;
                    }

                    var archiveId = ExtractArchiveIdFromReport(report);
                    var pathOrTitle = !string.IsNullOrWhiteSpace(report.Origin) && report.Origin.Contains('/')
                        ? report.Origin
                        : report.Title;

                    var searchSeriesCompatible = false;
                    if (searchCriteria?.Series != null)
                    {
                        if (!PlatformCompatibilityValidator.IsCompatible(report.Title, searchCriteria.Series.TitleSlug, searchCriteria.Series.Title, null, archiveId) ||
                            (!string.Equals(pathOrTitle, report.Title, StringComparison.OrdinalIgnoreCase) &&
                             !PlatformCompatibilityValidator.IsCompatible(pathOrTitle, searchCriteria.Series.TitleSlug, searchCriteria.Series.Title, null, archiveId)))
                        {
                            _logger.Debug("Skipping release '{0}' because an incorrect platform was detected for '{1}'", report.Title, searchCriteria.Series.Title);
                            reportNumber++;
                            continue;
                        }

                        searchSeriesCompatible = true;
                    }

                    parsedEpisodeInfo = TryParseRomRelease(report, searchCriteria, episodeLookup, searchSeriesCompatible, archiveId, pathOrTitle);

                    if (parsedEpisodeInfo == null)
                    {
                        parsedEpisodeInfo = Parser.Parser.ParseTitle(report.Title);

                        if (parsedEpisodeInfo == null || parsedEpisodeInfo.IsPossibleSpecialEpisode)
                        {
                            var specialEpisodeInfo = _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, report.Title, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);

                            if (specialEpisodeInfo != null)
                            {
                                parsedEpisodeInfo = specialEpisodeInfo;
                            }
                        }

                        parsedEpisodeInfo ??= Parser.Parser.ParseSeasonTitle(report.Title);
                    }

                    if (parsedEpisodeInfo != null && !parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                    {
                        var remoteEpisode = _parsingService.Map(parsedEpisodeInfo, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);
                        remoteEpisode.Release = report;
                        remoteEpisode.ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria);
                        remoteEpisode.ParsedRomInfo = report.ParsedRomInfo;

                        if (remoteEpisode.Series == null &&
                            searchCriteria?.Series != null &&
                            PlatformCompatibilityValidator.DetectPlatforms(report.Title)
                                .Any(p => PlatformCompatibilityValidator.IsPlatformMatch(
                                    p,
                                    PlatformCompatibilityValidator.NormalizePlatformSlug(searchCriteria.Series.TitleSlug, searchCriteria.Series.Title))))
                        {
                            remoteEpisode.Series = searchCriteria.Series;
                        }

                        if (searchCriteria == null &&
                            remoteEpisode.Series != null &&
                            remoteEpisode.Episodes.Empty() &&
                            _episodeService != null &&
                            !parsedEpisodeInfo.FullSeason)
                        {
                            rssEpisodeLookups ??= new Dictionary<int, SearchEpisodeTitleLookup>();
                            if (!rssEpisodeLookups.TryGetValue(remoteEpisode.Series.Id, out var rssLookup))
                            {
                                var platformEpisodes = _episodeService.GetEpisodeBySeries(remoteEpisode.Series.Id) ?? new List<Episode>();
                                rssLookup = new SearchEpisodeTitleLookup(platformEpisodes);
                                rssEpisodeLookups[remoteEpisode.Series.Id] = rssLookup;
                            }

                            var cleanRssTitle = CleanRomTitleForComparison(RomParser.ParseRomName(report.Title)?.GameTitle ?? report.Title);
                            var matchedEp = rssLookup.FindMatch(cleanRssTitle, parsedEpisodeInfo.SeasonNumber);
                            if (matchedEp != null)
                            {
                                remoteEpisode.Episodes = new List<Episode> { matchedEp };
                                parsedEpisodeInfo.SeasonNumber = matchedEp.SeasonNumber;
                                parsedEpisodeInfo.SeasonNumbers = new List<int> { matchedEp.SeasonNumber };
                                parsedEpisodeInfo.EpisodeNumbers = new[] { matchedEp.EpisodeNumber };
                                parsedEpisodeInfo.AbsoluteEpisodeNumbers = new[] { matchedEp.AbsoluteEpisodeNumber ?? matchedEp.EpisodeNumber };
                            }
                        }

                        if (remoteEpisode.Series == null)
                        {
                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownPlatform, "Unknown Platform"));
                        }
                        else if (remoteEpisode.Episodes.Empty())
                        {
                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownGame, "Unable to identify correct game(s) using release name"));
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

                    var fallbackParsedInfo = parsedEpisodeInfo ?? new ParsedEpisodeInfo
                    {
                        ReleaseTitle = report.Title,
                        SeriesTitle = searchCriteria?.Series?.Title ?? report.Title,
                        Languages = LanguageParser.ParseLanguages(report.Title),
                        Quality = QualityParser.ParseQuality(report.Title)
                    };

                    var remoteEpisode = new RemoteEpisode
                    {
                        Release = report,
                        ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria),
                        ParsedEpisodeInfo = fallbackParsedInfo,
                        Languages = fallbackParsedInfo.Languages,
                        Series = searchCriteria?.Series
                    };

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

        private static string ExtractArchiveIdFromReport(ReleaseInfo report)
        {
            return PlatformCompatibilityValidator.ExtractArchiveId(report?.InfoUrl);
        }

        private sealed class SearchEpisodeTitleLookup
        {
            private readonly Dictionary<string, Episode> _exactMatches = new(StringComparer.Ordinal);
            private readonly Dictionary<(string Title, int SeasonNumber), Episode> _regionMatches = new();

            public SearchEpisodeTitleLookup(IList<Episode> episodes)
            {
                foreach (var ep in episodes)
                {
                    if (ep == null || string.IsNullOrWhiteSpace(ep.Title))
                    {
                        continue;
                    }

                    var targetClean = CleanRomTitleForComparison(ep.Title);
                    if (string.IsNullOrEmpty(targetClean))
                    {
                        continue;
                    }

                    _exactMatches.TryAdd(targetClean, ep);
                    _regionMatches.TryAdd((targetClean, ep.SeasonNumber), ep);
                }
            }

            public Episode FindMatch(string cleanCandidate, int? preferredSeasonNumber = null)
            {
                if (string.IsNullOrEmpty(cleanCandidate))
                {
                    return null;
                }

                if (preferredSeasonNumber.HasValue &&
                    preferredSeasonNumber.Value > 0 &&
                    _regionMatches.TryGetValue((cleanCandidate, preferredSeasonNumber.Value), out var regionMatch))
                {
                    return regionMatch;
                }

                return _exactMatches.TryGetValue(cleanCandidate, out var exact) ? exact : null;
            }
        }

        private static SearchEpisodeTitleLookup BuildSearchEpisodeLookup(SearchCriteriaBase searchCriteria)
        {
            if (searchCriteria == null ||
                searchCriteria is SeasonSearchCriteria { IsPlatformCollectionSearch: true } ||
                searchCriteria.Episodes == null ||
                searchCriteria.Episodes.Count == 0)
            {
                return null;
            }

            return new SearchEpisodeTitleLookup(searchCriteria.Episodes);
        }

        private static ParsedEpisodeInfo TryParseRomRelease(
            ReleaseInfo report,
            SearchCriteriaBase searchCriteria,
            SearchEpisodeTitleLookup episodeLookup,
            bool compatibilityAlreadyVerified,
            string archiveId,
            string pathOrTitle)
        {
            var releaseTitle = report?.Title;
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                return null;
            }

            var fullContext = string.Join(
                " ",
                new[] { releaseTitle, report?.Origin, archiveId }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var romInfo = RomParser.ParseRomName(releaseTitle);
            var romInfoIsFullSet = romInfo?.IsFullSetArchive == true || report?.ParsedRomInfo?.IsFullSetArchive == true;
            var cleanRomGameTitle = CleanRomTitleForComparison(romInfo?.GameTitle ?? releaseTitle);

            if (searchCriteria != null)
            {
                var series = searchCriteria.Series;
                if (series != null)
                {
                    if (!compatibilityAlreadyVerified &&
                        (!PlatformCompatibilityValidator.IsCompatible(releaseTitle, series.TitleSlug, series.Title, null, archiveId) ||
                         (!string.Equals(pathOrTitle, releaseTitle, StringComparison.OrdinalIgnoreCase) &&
                          !PlatformCompatibilityValidator.IsCompatible(pathOrTitle, series.TitleSlug, series.Title, null, archiveId))))
                    {
                        return null;
                    }

                    var isCollectionSearch = searchCriteria is SeasonSearchCriteria { IsPlatformCollectionSearch: true };

                    // Check single game matches before pack/archive context matching so individual ROMs
                    // sourced from collection archives (HTGDB/No-Intro/Redump) are not rejected as full packs.
                    if (!isCollectionSearch && !romInfoIsFullSet && episodeLookup != null)
                    {
                        var matchingEp = episodeLookup.FindMatch(cleanRomGameTitle);

                        if (matchingEp != null)
                        {
                            var effectiveSeason = matchingEp.SeasonNumber;
                            var romQuality = romInfo?.Quality ?? report?.ParsedRomInfo?.Quality ?? new QualityModel(Quality.VerifiedGoodDump);
                            if (romQuality.Quality == Quality.Unknown)
                            {
                                romQuality = new QualityModel(Quality.VerifiedGoodDump);
                            }

                            var isTranslation = RegionHelper.IsTranslation(releaseTitle, releaseTitle, romInfo?.DumpTags) ||
                                                TranslationReleaseRegex.IsMatch(releaseTitle);
                            var isHack = !isTranslation &&
                                         (RegionHelper.IsRomHackOrTranslation(releaseTitle, releaseTitle, romInfo?.DumpTags) ||
                                          HackReleaseRegex.IsMatch(releaseTitle));

                            if ((isTranslation || isHack) &&
                                matchingEp.SeasonNumber != 5 &&
                                matchingEp.SeasonNumber != 6 &&
                                matchingEp.SeasonNumber != 9 &&
                                !HackReleaseRegex.IsMatch(matchingEp.Title ?? string.Empty) &&
                                !TranslationReleaseRegex.IsMatch(matchingEp.Title ?? string.Empty))
                            {
                                effectiveSeason = isTranslation ? 9 : 6;
                                if (isTranslation)
                                {
                                    romQuality = new QualityModel(Quality.TranslationPatch);
                                }
                            }
                            else
                            {
                                var isBetaProto = romQuality.Quality == Quality.BetaProto ||
                                                  BetaProtoDemoReleaseRegex.IsMatch(releaseTitle);

                                if (isBetaProto &&
                                    !BetaProtoDemoReleaseRegex.IsMatch(matchingEp.Title ?? string.Empty))
                                {
                                    romQuality = new QualityModel(Quality.BetaProto);
                                    if (matchingEp.SeasonNumber is >= 1 and <= 4)
                                    {
                                        effectiveSeason = 0;
                                    }
                                }
                                else if (matchingEp.SeasonNumber is >= 1 and <= 4)
                                {
                                    var explicitRegions = DetectExplicitRegions(releaseTitle, romInfo, matchingEp.Title);
                                    if (explicitRegions.Count > 0 &&
                                        !explicitRegions.Contains(matchingEp.SeasonNumber) &&
                                        !explicitRegions.Contains(4))
                                    {
                                        effectiveSeason = explicitRegions[0];
                                    }
                                }
                            }

                            return new ParsedEpisodeInfo
                            {
                                SeriesTitle = series.Title,
                                SeriesTitleInfo = new SeriesTitleInfo { Title = series.Title },
                                SeasonNumber = effectiveSeason,
                                SeasonNumbers = new List<int> { effectiveSeason },
                                EpisodeNumbers = new[] { matchingEp.EpisodeNumber },
                                AbsoluteEpisodeNumbers = new[] { matchingEp.AbsoluteEpisodeNumber ?? matchingEp.EpisodeNumber },
                                ReleaseTitle = releaseTitle,
                                Languages = LanguageParser.ParseLanguages(releaseTitle),
                                Quality = romQuality
                            };
                        }
                    }

                    // Check if this is a platform full set or collection pack
                    var isPackMatch = IsPlatformPackMatch(releaseTitle, fullContext, cleanRomGameTitle, series, compatibilityAlreadyVerified: true);
                    if (isPackMatch || isCollectionSearch || romInfoIsFullSet)
                    {
                        if (isPackMatch || romInfoIsFullSet)
                        {
                            var isPlatformCollection = isCollectionSearch || romInfoIsFullSet;

                            var allSeriesSeasons = series.Seasons?
                                .Select(s => s.SeasonNumber)
                                .Where(s => s > 0)
                                .Distinct()
                                .ToList() ?? new List<int>();

                            var targetSeason = searchCriteria is SeasonSearchCriteria ssc && ssc.SeasonNumber > 0 ? ssc.SeasonNumber : 1;
                            if (allSeriesSeasons.Count == 0)
                            {
                                allSeriesSeasons.Add(targetSeason);
                            }

                            var romQuality = romInfo?.Quality ?? report?.ParsedRomInfo?.Quality ?? new QualityModel(Quality.VerifiedGoodDump);
                            if (romQuality.Quality == Quality.Unknown)
                            {
                                romQuality = new QualityModel(Quality.VerifiedGoodDump);
                            }

                            var seasonNumbers = isPlatformCollection ? allSeriesSeasons : new List<int> { targetSeason };

                            return new ParsedEpisodeInfo
                            {
                                SeriesTitle = series.Title,
                                SeriesTitleInfo = new SeriesTitleInfo { Title = series.Title },
                                FullSeason = true,
                                SeasonNumber = seasonNumbers.First(),
                                SeasonNumbers = seasonNumbers,
                                ReleaseTitle = releaseTitle,
                                Languages = LanguageParser.ParseLanguages(releaseTitle),
                                Quality = romQuality
                            };
                        }
                    }
                }
            }
            else if (romInfo != null && !string.IsNullOrWhiteSpace(romInfo.GameTitle))
            {
                var ext = !string.IsNullOrWhiteSpace(romInfo.Format)
                    ? "." + romInfo.Format.TrimStart('.')
                    : (!string.IsNullOrWhiteSpace(report?.Container) ? "." + report.Container.TrimStart('.').ToLowerInvariant() : null);
                var platformName = PlatformCompatibilityValidator.ResolvePlatformName(releaseTitle, ext)
                                   ?? PlatformCompatibilityValidator.ResolvePlatformName(fullContext, ext);

                if (string.IsNullOrWhiteSpace(platformName))
                {
                    return null;
                }

                var seriesTitle = platformName;
                var seasonNum = romInfo.RegionNumber;
                var romQuality = romInfo.Quality ?? report?.ParsedRomInfo?.Quality ?? new QualityModel(Quality.VerifiedGoodDump);
                if (romQuality.Quality == Quality.Unknown)
                {
                    romQuality = new QualityModel(Quality.VerifiedGoodDump);
                }

                if (romInfoIsFullSet || IsFullSetRelease(releaseTitle))
                {
                    return new ParsedEpisodeInfo
                    {
                        SeriesTitle = seriesTitle,
                        SeriesTitleInfo = new SeriesTitleInfo { Title = seriesTitle },
                        FullSeason = true,
                        SeasonNumber = 1,
                        SeasonNumbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 },
                        ReleaseTitle = releaseTitle,
                        Languages = LanguageParser.ParseLanguages(releaseTitle),
                        Quality = romQuality
                    };
                }

                return new ParsedEpisodeInfo
                {
                    SeriesTitle = seriesTitle,
                    SeriesTitleInfo = new SeriesTitleInfo { Title = seriesTitle },
                    ReleaseTitle = releaseTitle,
                    SeasonNumber = seasonNum,
                    SeasonNumbers = new List<int> { seasonNum },
                    Languages = LanguageParser.ParseLanguages(releaseTitle),
                    Quality = romQuality
                };
            }

            return null;
        }

        private static readonly Regex ParentheticalRegionTagRegex = new(
            @"\((?:[^)]*\b(?:USA|Europe|Japan|World|Australia|Canada|Brazil|Korea|China|Taiwan|Asia|France|Germany|Spain|Italy|Netherlands|Sweden|Denmark|Norway|Finland|Portugal|Russia|Poland|Greece|En|Ja|Fr|De|Es|It|Nl|Pt|Sv|No|Da|Fi|Zh|Ko)\b[^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool IsFullSetRelease(string releaseTitle)
        {
            var text = releaseTitle ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.Contains("SMDB", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("HTGDB", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("FlashBoy", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("EverDrive", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Ever Drive", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("PowerPak", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("RetroHQ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Darksoft", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("GamePack", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Full Set", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("1G1R", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("No-Intro", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("nointro", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Redump", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("TOSEC", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Romset", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Rom Set", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("@", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("[", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !ParentheticalRegionTagRegex.IsMatch(text) &&
                   (text.Contains("Complete Collection", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Complete Set", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Complete Rom", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Full Collection", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Rom Collection", StringComparison.OrdinalIgnoreCase));
        }

        private static readonly ConcurrentDictionary<string, Regex> WordBoundaryRegexCache = new(StringComparer.OrdinalIgnoreCase);

        private static Regex GetWordBoundaryRegex(string token)
        {
            return WordBoundaryRegexCache.GetOrAdd(
                token,
                t => new Regex($@"(?<![A-Za-z0-9]){Regex.Escape(t)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        private static bool IsPlatformPackMatch(string releaseTitle, string fullContext, string cleanRomGameTitle, NzbDrone.Core.Tv.Series series, bool compatibilityAlreadyVerified = false)
        {
            if (series == null || string.IsNullOrWhiteSpace(releaseTitle))
            {
                return false;
            }

            var contextToCheck = !string.IsNullOrWhiteSpace(fullContext) ? fullContext : releaseTitle;

            if (!IsFullSetRelease(releaseTitle))
            {
                return false;
            }

            if (!compatibilityAlreadyVerified && !PlatformCompatibilityValidator.IsCompatible(releaseTitle, series.TitleSlug, series.Title))
            {
                return false;
            }

            var cleanSeriesTitle = CleanRomTitleForComparison(series.Title);
            if (IsRomTitleMatch(cleanRomGameTitle, cleanSeriesTitle) ||
                contextToCheck.Contains(series.Title, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var slug = series.TitleSlug?.ToLowerInvariant() ?? string.Empty;
            var targetSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(slug, series.Title);
            if (!string.IsNullOrWhiteSpace(targetSlug) &&
                PlatformCompatibilityValidator.DetectPlatforms(contextToCheck).Any(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(slug) && slug.Length >= 2 &&
                GetWordBoundaryRegex(slug).IsMatch(contextToCheck))
            {
                return true;
            }

            var aliases = targetSlug switch
            {
                "snes" => new[] { "snes", "sd2snes", "super nintendo", "super famicom", "sfc", "super everdrive" },
                "nes" => new[] { "nes", "nes2.0", "everdrive n8", "famicom", "nintendo entertainment system" },
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
                "sms" => new[] { "master everdrive", "master system", "sega master system", "sms" },
                "gg" => new[] { "game gear", "gamegear", "everdrive gg" },
                "tg16" => new[] { "turbo everdrive", "pc engine", "turbografx", "pce" },
                "tgcd" => new[] { "tgcd", "turbografx-cd", "pc engine cd" },
                "atari2600" => new[] { "harmony", "atari 2600" },
                "atari5200" => new[] { "atari 5200" },
                "atari7800" => new[] { "atari 7800" },
                "lynx" => new[] { "atari lynx", "lynx" },
                "jaguar" => new[] { "atari jaguar", "jaguar" },
                "fds" => new[] { "fds", "famicom disk system" },
                "vb" => new[] { "virtual boy", "flashboy", "vb" },
                "wonderswan" => new[] { "wonderswan" },
                "neogeo" => new[] { "neosd", "neo geo", "neogeo" },
                _ => PlatformCompatibilityValidator.GetAliasesForPlatform(targetSlug)
            };

            return aliases.Any(a => a.Length >= 2 && GetWordBoundaryRegex(a).IsMatch(contextToCheck));
        }

        private static readonly Regex TrailingSceneGroupDotRegex = new(
            @"(?<=\S)-[A-Za-z0-9]{2,12}$",
            RegexOptions.Compiled);

        private static readonly Regex VersionWordStripRegex = new(
            @"(?<![A-Za-z0-9])Version(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SceneTagsRegex = new(
            @"(?<![A-Za-z0-9])(USA|EUR|JPN|ASIA|WORLD|PAL|NTSC|NTSC-U|NTSC-J|AUS|CAN|KOR|CHN|TWN|UK|GER|FRA|ITA|SPA|3DS|CIA|CCI|NDS|DSI|GBA|GBC|GB|NES|SNES|SFC|SMC|N64|Z64|V64|NSW|SWITCH|PS1|PSX|PS2|PS3|PS4|PS5|PSP|PSVITA|WII|WIIU|XBOX|XBOX360|ISO|ROM|CHD|RVZ|CUE|BIN)(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SceneGroupSuffixRegex = new(
            @"(?<![A-Za-z0-9])(USA|EUR|JPN|ASIA|WORLD|GER|FRA|ITA|SPA|3DS|CIA|CCI|NDS|DSI|GBA|GBC|GB|NES|SNES|SFC|SMC|N64|Z64|V64|NSW|SWITCH|PS1|PSX|PS2|PS3|PS4|PS5|PSP|PSVITA|WII|WIIU|XBOX|XBOX360|XBOXDVD|ISO|ROM|CHD|RVZ|CUE|BIN)\s*-[A-Za-z0-9]{2,12}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PlatformWordsStripRegex = new(
            @"(?<![A-Za-z0-9])(?:Game\s*Boy\s+Advance|Game\s*Boy\s+Color|Game\s*Boy|Super\s+Nintendo(?:\s+Entertainment\s+System)?|Nintendo\s+Entertainment\s+System|Nintendo\s+64DD|Nintendo\s+64|Nintendo\s+GameCube|GameCube|Nintendo\s+Wii\s*U|Nintendo\s+Wii|Nintendo\s+Switch|Nintendo\s+3DS|Nintendo\s+DS|Nintendo\s+(?:3DS|DS|Switch|GBA|GBC|GB|NES|SNES|N64|WiiU?|GC)|Virtual\s+Boy|PlayStation\s+Portable|PlayStation\s+Vita|Sony\s+PlayStation(?:\s*[12345])?|Sony\s+(?:PS[1-5]|PSP|PSV(?:ita)?)|Sega\s+Genesis|Mega\s+Drive|Master\s+System|Game\s+Gear|Sega\s+Saturn|Sega\s+Dreamcast|Sega\s+CD|Mega\s*CD|Sega\s+32X|Sega\s+(?:CD|DC|MD|SMS|GG|Saturn|Dreamcast|Genesis)|SG-1000|Microsoft\s+Xbox(?:\s*360|\s*One)?|Atari\s+Lynx|Atari\s+Jaguar(?:\s+CD)?|Atari\s+(?:2600|5200|7800)|TurboGrafx(?:-|\s*)16|PC\s+Engine|PC-FX|Philips\s+CD-?i|CD-?i|3DO(?:\s+Interactive\s+Multiplayer)?|Amiga\s+CD32|ColecoVision|Intellivision|Vectrex|Neo\s*Geo(?:\s+(?:CD|Pocket(?:\s+Color)?))?|WonderSwan(?:\s+Color)?|Famicom\s+Disk\s+System)(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TrailingDashRegionOrReleaseRegex = new(
            @"(?:\s*-\s*(?:USA|EUR|Europe|Japan|JPN|World|Australia|Canada|France|Germany|Spain|Italy|Netherlands|Sweden|Denmark|Norway|Finland|Portugal|Brazil|UK|PAL|NTSC|Release|Demonstration\s+Disc)?\s*)+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BenignRomTokensRegex = new(
            @"(?<![A-Za-z0-9])(?:Rev\s*[A-Za-z0-9.]+|v\d+(?:\.\d+)*|Trimmed|Multi\d*|iNTERNAL|PROPER|REPACK|DUMP|CLEAN|EU-NL|PT-BR)(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HackReleaseRegex = new(
            @"\b(?:kaizo|rom\s*hack|hack(?:ed)?|anti[\s._-]*piracy(?:\s+patch)?|patched|randomizer|redux|deluxe\s+hack|volt\s+white|blaze\s+black|renegade\s+platinum)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TranslationReleaseRegex = new(
            @"\b(?:fan[\s._-]*translat(?:ed|ion)|translat(?:ed|ion)|pt[\s._-]*br)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BetaProtoDemoReleaseRegex = new(
            @"\b(?:beta|proto(?:types?)?|demo|shareware|kiosk|sample)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StandaloneRegionTokenRegex = new(
            @"(?<![A-Za-z0-9])(?:USA|EUR|Europe|EU-NL|JPN|Japan|PT-BR|FRA|France|GER|Germany|SPA|Spain|ITA|Italy|KOR|Korea|CHN|China|AUS|Australia|PAL|NTSC-U|NTSC-J)(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static List<int> DetectExplicitRegions(string releaseTitle, ParsedRomInfo romInfo, string targetGameTitle)
        {
            var detected = new List<int>();

            if (romInfo?.Regions != null)
            {
                foreach (var region in romInfo.Regions)
                {
                    var mapped = TryMapExplicitRegionToken(region);
                    if (mapped >= 0 && !detected.Contains(mapped))
                    {
                        detected.Add(mapped);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(releaseTitle))
            {
                var safeTargetTitle = targetGameTitle ?? string.Empty;
                foreach (Match match in StandaloneRegionTokenRegex.Matches(releaseTitle))
                {
                    var token = match.Value;
                    if (!string.IsNullOrWhiteSpace(safeTargetTitle) &&
                        safeTargetTitle.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var mapped = TryMapExplicitRegionToken(token);
                    if (mapped >= 0 && !detected.Contains(mapped))
                    {
                        detected.Add(mapped);
                    }
                }
            }

            return detected;
        }

        private static int TryMapExplicitRegionToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return -1;
            }

            return token.Trim().ToLowerInvariant() switch
            {
                "usa" or "us" or "u" or "canada" or "ntsc-u" => 1,
                "eur" or "europe" or "eu" or "e" or "eu-nl" or "pal" or "uk" or "united kingdom" or "australia" or "aus" or "france" or "fra" or "germany" or "ger" or "spain" or "spa" or "italy" or "ita" or "netherlands" or "belgium" or "austria" or "switzerland" or "sweden" or "denmark" or "finland" or "norway" or "portugal" or "russia" or "poland" or "greece" or "scandinavia" => 2,
                "jpn" or "japan" or "jp" or "j" or "ntsc-j" or "asia" or "korea" or "kor" or "china" or "chn" or "taiwan" or "hong kong" => 3,
                "world" or "w" or "jue" or "uej" => 4,
                "pt-br" => 9,
                _ => -1
            };
        }

        private static string CleanRomTitleForComparison(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var clean = RomParser.CleanIndexPrefix(title.Trim()).RemoveDiacritics();
            clean = clean.Replace('.', ' ').Replace('_', ' ');

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

            // Strip scene release group tag at end: e.g. "USA 3DS-SUXXORS" or "N64-DUMP"
            clean = SceneGroupSuffixRegex.Replace(clean, "$1");
            if (!title.Contains(' ') && title.Contains('.'))
            {
                clean = TrailingSceneGroupDotRegex.Replace(clean, string.Empty);
            }

            var withoutPlatformWords = PlatformWordsStripRegex.Replace(clean, " ").Trim();
            if (withoutPlatformWords.Any(char.IsLetterOrDigit))
            {
                clean = withoutPlatformWords;
            }

            var withoutTrailingDash = TrailingDashRegionOrReleaseRegex.Replace(clean, string.Empty).Trim();
            if (withoutTrailingDash.Any(char.IsLetterOrDigit))
            {
                clean = withoutTrailingDash;
            }

            var withoutSceneTags = SceneTagsRegex.Replace(clean, " ").Trim();
            if (withoutSceneTags.Any(char.IsLetterOrDigit))
            {
                clean = withoutSceneTags;
            }

            var withoutBenign = BenignRomTokensRegex.Replace(clean, " ").Trim();
            if (withoutBenign.Any(char.IsLetterOrDigit))
            {
                clean = withoutBenign;
            }

            var withoutVersionWord = VersionWordStripRegex.Replace(clean, " ").Trim();
            if (withoutVersionWord.Any(char.IsLetterOrDigit))
            {
                clean = withoutVersionWord;
            }

            clean = RomParser.NormalizeTitleArticle(clean.Trim());

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
            var candidateClean = CleanRomTitleForComparison(cleanCandidate);
            var targetClean = CleanRomTitleForComparison(rawTarget);
            if (string.IsNullOrWhiteSpace(candidateClean) || string.IsNullOrWhiteSpace(targetClean))
            {
                return false;
            }

            if (candidateClean.Equals(targetClean, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (candidateClean.Length >= 3 && targetClean.Length >= 3)
            {
                return candidateClean.StartsWith(targetClean, StringComparison.OrdinalIgnoreCase) ||
                       targetClean.StartsWith(candidateClean, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private DownloadDecision GetDecisionForReport(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            List<DownloadRejection> reasons = null;

            foreach (var specifications in _orderedSpecificationGroups)
            {
                foreach (var specification in specifications)
                {
                    var rejection = EvaluateSpec(specification, remoteEpisode, information);
                    if (rejection != null)
                    {
                        reasons ??= new List<DownloadRejection>();
                        reasons.Add(rejection);
                    }
                }

                if (reasons != null && reasons.Count > 0)
                {
                    break;
                }
            }

            return new DownloadDecision(remoteEpisode, reasons?.ToArray() ?? Array.Empty<DownloadRejection>());
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
