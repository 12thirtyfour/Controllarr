using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.CustomFormats.Events;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Profiles.Qualities
{
    public interface IQualityProfileService
    {
        QualityProfile Add(QualityProfile profile);
        void Update(QualityProfile profile);
        void Delete(int id);
        List<QualityProfile> All();
        QualityProfile Get(int id);
        bool Exists(int id);
        QualityProfile GetDefaultProfile(string name, Quality cutoff = null, params Quality[] allowed);
        void UpdateAllSizeLimits(params QualityProfileSizeLimit[] sizeLimits);
    }

    public class QualityProfileService : IQualityProfileService,
                                         IHandle<ApplicationStartedEvent>,
                                         IHandle<CustomFormatAddedEvent>,
                                         IHandle<CustomFormatDeletedEvent>
    {
        private readonly IQualityProfileRepository _qualityProfileRepository;
        private readonly IImportListFactory _importListFactory;
        private readonly ICustomFormatService _formatService;
        private readonly ISeriesService _seriesService;
        private readonly IQualityProfileRankService _rankService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public QualityProfileService(IQualityProfileRepository qualityProfileRepository,
                                     IImportListFactory importListFactory,
                                     ICustomFormatService formatService,
                                     ISeriesService seriesService,
                                     IQualityProfileRankService rankService,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _qualityProfileRepository = qualityProfileRepository;
            _importListFactory = importListFactory;
            _formatService = formatService;
            _seriesService = seriesService;
            _rankService = rankService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public QualityProfile Add(QualityProfile profile)
        {
            var saved = _qualityProfileRepository.Insert(profile);
            _rankService.UpdateRanksForProfile(saved);
            return saved;
        }

        public void Update(QualityProfile profile)
        {
            _qualityProfileRepository.Update(profile);
            _rankService.UpdateRanksForProfile(profile);
            _eventAggregator.PublishEvent(new QualityProfileUpdatedEvent(profile.Id));
        }

        public void Delete(int id)
        {
            if (_seriesService.GetAllSeries().Any(c => c.QualityProfileId == id) || _importListFactory.All().Any(c => c.QualityProfileId == id))
            {
                var profile = _qualityProfileRepository.Get(id);
                throw new QualityProfileInUseException(profile.Name);
            }

            _qualityProfileRepository.Delete(id);
            _rankService.DeleteRanksForProfile(id);
        }

        public List<QualityProfile> All()
        {
            return _qualityProfileRepository.All().ToList();
        }

        public QualityProfile Get(int id)
        {
            var profile = _qualityProfileRepository.Find(id);
            return profile ?? GetDefaultProfile("Default");
        }

        public bool Exists(int id)
        {
            return true;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            var profiles = All();

            if (profiles.Any())
            {
                var gamingQualities = new[]
                {
                    Quality.VerifiedGoodDump,
                    Quality.VerifiedRedump,
                    Quality.NoIntroVerified,
                    Quality.UnverifiedRaw,
                    Quality.BetaProto,
                    Quality.TranslationPatch,
                    Quality.Unknown
                };

                foreach (var profile in profiles)
                {
                    var modified = false;

                    foreach (var gq in gamingQualities)
                    {
                        var qualityItem = profile.Items?.FirstOrDefault(i => i.Quality == gq) ??
                                          profile.Items?.SelectMany(i => i.Items ?? new List<QualityProfileQualityItem>()).FirstOrDefault(i => i.Quality == gq);

                        if (qualityItem != null)
                        {
                            if (!qualityItem.Allowed && (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Equals("Any", StringComparison.OrdinalIgnoreCase)))
                            {
                                qualityItem.Allowed = true;
                                modified = true;
                            }
                        }
                        else
                        {
                            if (profile.Items == null)
                            {
                                profile.Items = new List<QualityProfileQualityItem>();
                            }

                            profile.Items.Insert(0, new QualityProfileQualityItem { Quality = gq, Allowed = true });
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        if (profile.Cutoff == 0 || profile.Cutoff == Quality.SDTV.Id)
                        {
                            profile.Cutoff = Quality.VerifiedGoodDump.Id;
                        }

                        Update(profile);
                    }
                }

                _rankService.SeedAll(profiles);

                return;
            }

            _logger.Info("Setting up default quality profiles");

            AddDefaultProfile("Any",
                Quality.VerifiedGoodDump,
                Quality.VerifiedGoodDump,
                Quality.VerifiedRedump,
                Quality.NoIntroVerified,
                Quality.UnverifiedRaw,
                Quality.BetaProto,
                Quality.TranslationPatch,
                Quality.Unknown);

            AddDefaultProfile("Verified Dumps",
                Quality.VerifiedGoodDump,
                Quality.VerifiedGoodDump,
                Quality.VerifiedRedump,
                Quality.NoIntroVerified);

            AddDefaultProfile("Retail Dumps",
                Quality.VerifiedGoodDump,
                Quality.VerifiedGoodDump,
                Quality.VerifiedRedump,
                Quality.NoIntroVerified,
                Quality.UnverifiedRaw);

            AddDefaultProfile("Homebrew & Hacks",
                Quality.UnverifiedRaw,
                Quality.UnverifiedRaw,
                Quality.BetaProto,
                Quality.TranslationPatch,
                Quality.Unknown);

            AddDefaultProfile("Clean ROMs",
                Quality.NoIntroVerified,
                Quality.VerifiedGoodDump,
                Quality.NoIntroVerified);

            AddDefaultProfile("Optical Discs",
                Quality.VerifiedRedump,
                Quality.VerifiedRedump);
        }

        public void Handle(CustomFormatAddedEvent message)
        {
            var all = All();

            foreach (var profile in all)
            {
                profile.FormatItems.Insert(0, new ProfileFormatItem
                {
                    Score = 0,
                    Format = message.CustomFormat
                });

                Update(profile);
            }
        }

        public void Handle(CustomFormatDeletedEvent message)
        {
            var all = All();
            foreach (var profile in all)
            {
                profile.FormatItems = profile.FormatItems.Where(c => c.Format.Id != message.CustomFormat.Id).ToList();

                if (profile.FormatItems.Empty())
                {
                    profile.MinFormatScore = 0;
                    profile.CutoffFormatScore = 0;
                    profile.MinUpgradeFormatScore = 1;
                }

                Update(profile);
            }
        }

        public QualityProfile GetDefaultProfile(string name, Quality cutoff = null, params Quality[] allowed)
        {
            var groupedQualites = Quality.DefaultQualityDefinitions.GroupBy(q => q.Weight).OrderBy(g => g.Key);
            var items = new List<QualityProfileQualityItem>();
            var groupId = 1000;
            var profileCutoff = cutoff == null ? Quality.Unknown.Id : cutoff.Id;

            foreach (var group in groupedQualites)
            {
                if (group.Count() == 1)
                {
                    var quality = group.First().Quality;

                    items.Add(new QualityProfileQualityItem
                    {
                        Quality = group.First().Quality,
                        Allowed = allowed.Contains(quality),
                        MinSize = group.First().MinSize,
                        MaxSize = group.First().MaxSize,
                        PreferredSize = group.First().PreferredSize
                    });
                    continue;
                }

                var groupAllowed = group.Any(g => allowed.Contains(g.Quality));

                items.Add(new QualityProfileQualityItem
                {
                    Id = groupId,
                    Name = group.First().GroupName,
                    Items = group.Select(g => new QualityProfileQualityItem
                    {
                        Quality = g.Quality,
                        Allowed = groupAllowed,
                        MinSize = g.MinSize,
                        MaxSize = g.MaxSize,
                        PreferredSize = g.PreferredSize
                    }).ToList(),
                    Allowed = groupAllowed
                });

                if (group.Any(g => g.Quality.Id == profileCutoff))
                {
                    profileCutoff = groupId;
                }

                groupId++;
            }

            var formatItems = _formatService.All().Select(format => new ProfileFormatItem
            {
                Score = 0,
                Format = format
            }).ToList();

            var qualityProfile = new QualityProfile
                                 {
                                     Name = name,
                                     Cutoff = profileCutoff,
                                     Items = items,
                                     MinFormatScore = 0,
                                     CutoffFormatScore = 0,
                                     MinUpgradeFormatScore = 1,
                                     FormatItems = formatItems
                                 };

            return qualityProfile;
        }

        public void UpdateAllSizeLimits(params QualityProfileSizeLimit[] sizeLimits)
        {
            var all = All();

            foreach (var qualityProfile in all)
            {
                foreach (var sizeLimit in sizeLimits)
                {
                        var qualityIndex = qualityProfile.GetIndex(sizeLimit.Quality, true);
                        var qualityOrGroup = qualityProfile.Items[qualityIndex.Index];
                        var item = qualityOrGroup.Quality == null ? qualityOrGroup.Items[qualityIndex.GroupIndex] : qualityOrGroup;

                        item.MinSize = sizeLimit.MinSize;
                        item.MaxSize = sizeLimit.MaxSize;
                        item.PreferredSize = sizeLimit.PreferredSize;
                }
            }

            _qualityProfileRepository.UpdateMany(all);
        }

        private QualityProfile AddDefaultProfile(string name, Quality cutoff, params Quality[] allowed)
        {
            var profile = GetDefaultProfile(name, cutoff, allowed);

            return Add(profile);
        }
    }
}
