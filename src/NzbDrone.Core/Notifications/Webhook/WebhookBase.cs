using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Webhook
{
    public abstract class WebhookBase<TSettings> : NotificationBase<TSettings>
        where TSettings : NotificationSettingsBase<TSettings>, new()
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        protected readonly ILocalizationService _localizationService;
        private readonly ITagRepository _tagRepository;
        private readonly IMapCoversToLocal _mediaCoverService;

        protected WebhookBase(IConfigFileProvider configFileProvider, IConfigService configService, ILocalizationService localizationService, ITagRepository tagRepository, IMapCoversToLocal mediaCoverService)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _localizationService = localizationService;
            _tagRepository = tagRepository;
            _mediaCoverService = mediaCoverService;
        }

        protected WebhookGrabPayload BuildOnGrabPayload(GrabMessage message)
        {
            var remoteGame = message.Episode;
            var quality = message.Quality;

            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Grab,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(message.Series),
                Games = remoteGame.Episodes.ConvertAll(x => new WebhookGame(x)),
                Release = new WebhookRelease(quality, remoteGame),
                DownloadClient = message.DownloadClientName,
                DownloadClientType = message.DownloadClientType,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(remoteGame.CustomFormats, remoteGame.CustomFormatScore),
            };
        }

        protected WebhookImportPayload BuildOnDownloadPayload(DownloadMessage message)
        {
            var romFile = message.EpisodeFile;

            var payload = new WebhookImportPayload
            {
                EventType = WebhookEventType.Download,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(message.Series),
                Games = romFile.Episodes.Value.ConvertAll(x => new WebhookGame(x)),
                RomFile = new WebhookRomFile(romFile)
                {
                    SourcePath = message.SourcePath
                },
                Release = new WebhookGrabbedRelease(message.Release, romFile.IndexerFlags, romFile.ReleaseType),
                IsUpgrade = message.OldFiles.Any(),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(message.EpisodeInfo.CustomFormats, message.EpisodeInfo.CustomFormatScore)
            };

            if (message.OldFiles.Any())
            {
                payload.DeletedFiles = message.OldFiles.ConvertAll(x => new WebhookRomFile(x.EpisodeFile)
                {
                    Path = Path.Combine(message.Series.Path, x.EpisodeFile.RelativePath),
                    RecycleBinPath = x.RecycleBinPath
                });
            }

            return payload;
        }

        protected WebhookImportCompletePayload BuildOnImportCompletePayload(ImportCompleteMessage message)
        {
            var romFiles = message.EpisodeFiles;

            var payload = new WebhookImportCompletePayload
            {
                EventType = WebhookEventType.Download,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(message.Series),
                Games = message.Episodes.ConvertAll(x => new WebhookGame(x)),
                RomFiles = romFiles.ConvertAll(e => new WebhookRomFile(e)),
                Release = new WebhookGrabbedRelease(message.Release, romFiles.First().IndexerFlags, romFiles.First().ReleaseType),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                SourcePath = message.SourcePath,
                DestinationPath = message.DestinationPath
            };

            return payload;
        }

        protected WebhookGameDeletePayload BuildOnEpisodeFileDelete(GameDeleteMessage deleteMessage)
        {
            return new WebhookGameDeletePayload
            {
                EventType = WebhookEventType.RomFileDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(deleteMessage.Series),
                Games = deleteMessage.EpisodeFile.Episodes.Value.ConvertAll(x => new WebhookGame(x)),
                RomFile = new WebhookRomFile(deleteMessage.EpisodeFile),
                DeleteReason = deleteMessage.Reason
            };
        }

        protected WebhookPlatformAddPayload BuildOnSeriesAdd(PlatformAddMessage addMessage)
        {
            return new WebhookPlatformAddPayload
            {
                EventType = WebhookEventType.PlatformAdd,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(addMessage.Series),
            };
        }

        protected WebhookPlatformDeletePayload BuildOnSeriesDelete(PlatformDeleteMessage deleteMessage)
        {
            return new WebhookPlatformDeletePayload
            {
                EventType = WebhookEventType.PlatformDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(deleteMessage.Series),
                DeletedFiles = deleteMessage.DeletedFiles
            };
        }

        protected WebhookRenamePayload BuildOnRenamePayload(Series series, List<RenamedRomFile> renamedFiles)
        {
            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(series),
                RenamedRomFiles = renamedFiles.ConvertAll(x => new WebhookRenamedRomFile(x))
            };
        }

        protected WebhookHealthPayload BuildHealthPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.Health,
                InstanceName = _configFileProvider.InstanceName,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookHealthPayload BuildHealthRestoredPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.HealthRestored,
                InstanceName = _configFileProvider.InstanceName,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookApplicationUpdatePayload BuildApplicationUpdatePayload(ApplicationUpdateMessage updateMessage)
        {
            return new WebhookApplicationUpdatePayload
            {
                EventType = WebhookEventType.ApplicationUpdate,
                InstanceName = _configFileProvider.InstanceName,
                Message = updateMessage.Message,
                PreviousVersion = updateMessage.PreviousVersion.ToString(),
                NewVersion = updateMessage.NewVersion.ToString()
            };
        }

        protected WebhookManualInteractionPayload BuildManualInteractionRequiredPayload(ManualInteractionRequiredMessage message)
        {
            var remoteGame = message.Episode;
            var quality = message.Quality;

            return new WebhookManualInteractionPayload
            {
                EventType = WebhookEventType.ManualInteractionRequired,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = GetPlatform(message.Series),
                Games = remoteGame.Episodes.ConvertAll(x => new WebhookGame(x)),
                DownloadInfo = new WebhookDownloadClientItem(quality, message.TrackedDownload.DownloadItem),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                DownloadStatus = message.TrackedDownload.Status.ToString(),
                DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList(),
                CustomFormatInfo = new WebhookCustomFormatInfo(remoteGame.CustomFormats, remoteGame.CustomFormatScore),
                Release = new WebhookGrabbedRelease(message.Release)
            };
        }

        protected WebhookPayload BuildTestPayload()
        {
            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Test,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Platform = new WebhookPlatform
                {
                    Id = 1,
                    Title = "Test Platform",
                    Path = "C:\\testpath",
                    CatalogId = 1234,
                    Tags = new List<string> { "test-tag" }
                },
                Games = new List<WebhookGame>
                {
                    new()
                    {
                        Id = 123,
                        PlatformId = 1,
                        RegionNumber = 1,
                        Region = "USA",
                        GameNumber = 1,
                        Title = "Test Game"
                    }
                }
            };
        }

        private WebhookPlatform GetPlatform(Series series)
        {
            if (series == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(series.Id, series.Images, series.Added);

            return new WebhookPlatform(series, GetTagLabels(series));
        }

        private List<string> GetTagLabels(Series series)
        {
            if (series == null)
            {
                return null;
            }

            return _tagRepository.GetTags(series.Tags)
                .Select(s => s.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
