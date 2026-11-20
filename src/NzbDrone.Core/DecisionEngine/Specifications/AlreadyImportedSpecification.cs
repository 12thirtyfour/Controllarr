using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class AlreadyImportedSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IHistoryService _historyService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public AlreadyImportedSpecification(IHistoryService historyService,
                                            IConfigService configService,
                                            Logger logger)
        {
            _historyService = historyService;
            _configService = configService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Database;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            var cdhEnabled = _configService.EnableCompletedDownloadHandling;

            if (!cdhEnabled)
            {
                _logger.Debug("Skipping already imported check because CDH is disabled");
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Performing already imported check on report");
            var episodesWithFiles = subject.Episodes.Where(e => e.HasFile).ToList();
            if (episodesWithFiles.Count == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            Dictionary<int, List<GameHistory>> historyByEpisode = null;
            if (episodesWithFiles.Count > 5 && subject.Series != null)
            {
                var targetEpisodeIds = episodesWithFiles.Select(e => e.Id).ToHashSet();
                historyByEpisode = new Dictionary<int, List<GameHistory>>();
                foreach (var entry in _historyService.GetBySeries(subject.Series.Id, null))
                {
                    if (!targetEpisodeIds.Contains(entry.EpisodeId))
                    {
                        continue;
                    }

                    if (!historyByEpisode.TryGetValue(entry.EpisodeId, out var list))
                    {
                        list = new List<GameHistory>();
                        historyByEpisode[entry.EpisodeId] = list;
                    }

                    list.Add(entry);
                }

                if (historyByEpisode.Count == 0)
                {
                    return DownloadSpecDecision.Accept();
                }
            }

            foreach (var episode in episodesWithFiles)
            {
                List<GameHistory> historyForEpisode;
                if (historyByEpisode != null)
                {
                    if (!historyByEpisode.TryGetValue(episode.Id, out historyForEpisode))
                    {
                        continue;
                    }
                }
                else
                {
                    historyForEpisode = _historyService.FindByEpisodeId(episode.Id);
                }

                var lastGrabbed = historyForEpisode.FirstOrDefault(h => h.EventType == GameHistoryEventType.Grabbed);

                if (lastGrabbed == null)
                {
                    continue;
                }

                var imported = historyForEpisode.FirstOrDefault(h =>
                    h.EventType == GameHistoryEventType.DownloadFolderImported &&
                    h.DownloadId == lastGrabbed.DownloadId);

                if (imported == null)
                {
                    continue;
                }

                // This is really only a guard against redownloading the same release over
                // and over when the grabbed and imported qualities do not match, if they do
                // match skip this check.
                if (lastGrabbed.Quality.Equals(imported.Quality))
                {
                    continue;
                }

                var release = subject.Release;

                if (release.DownloadProtocol == DownloadProtocol.Torrent)
                {
                    var torrentInfo = release as TorrentInfo;

                    if (torrentInfo?.InfoHash != null && torrentInfo.InfoHash.ToUpper() == lastGrabbed.DownloadId)
                    {
                        _logger.Debug("Has same torrent hash as a grabbed and imported release");
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.AlreadyImportedSameHash, "Has same torrent hash as a grabbed and imported release");
                    }
                }

                // Only based on title because a release with the same title on another indexer/released at
                // a different time very likely has the exact same content and we don't need to also try it.

                if (release.Title.Equals(lastGrabbed.SourceTitle, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Debug("Has same release name as a grabbed and imported release");
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.AlreadyImportedSameName, "Has same release name as a grabbed and imported release");
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
