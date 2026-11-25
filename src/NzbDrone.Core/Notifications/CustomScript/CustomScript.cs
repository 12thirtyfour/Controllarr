using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Games;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tags;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.CustomScript
{
    public class CustomScript : NotificationBase<CustomScriptSettings>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly IProcessProvider _processProvider;
        private readonly ITagRepository _tagRepository;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public CustomScript(IConfigFileProvider configFileProvider,
            IConfigService configService,
            IDiskProvider diskProvider,
            IProcessProvider processProvider,
            ITagRepository tagRepository,
            ILocalizationService localizationService,
            Logger logger)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _diskProvider = diskProvider;
            _processProvider = processProvider;
            _tagRepository = tagRepository;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Name => _localizationService.GetLocalizedString("NotificationsCustomScriptSettingsName");

        public override string Link => "https://github.com/12thirtyfour/Controllarr/wiki";

        public override ProviderMessage Message => new ProviderMessage(_localizationService.GetLocalizedString("NotificationsCustomScriptSettingsProviderMessage", new Dictionary<string, object> { { "eventTypeTest", "Test" } }), ProviderMessageType.Warning);

        public override void OnGrab(GrabMessage message)
        {
            var platform = message.Series;
            var remoteGame = message.Episode;
            var releaseGroup = remoteGame.ParsedEpisodeInfo.ReleaseGroup;
            var regionNumber = remoteGame.Episodes.FirstOrDefault()?.SeasonNumber ?? 0;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Grab");
            AddPlatformVariables(environmentVariables, platform);

            environmentVariables.Add("Controllarr_Release_GameCount", remoteGame.Episodes.Count.ToString());
            environmentVariables.Add("Controllarr_Release_RegionNumber", regionNumber.ToString());
            environmentVariables.Add("Controllarr_Release_Region", RegionHelper.GetRegionName(regionNumber));
            environmentVariables.Add("Controllarr_Release_GameNumbers", string.Join(",", remoteGame.Episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Controllarr_Release_GameTitles", string.Join("|", remoteGame.Episodes.Select(e => e.Title)));
            environmentVariables.Add("Controllarr_Release_GameOverviews", string.Join("|", remoteGame.Episodes.Select(e => e.Overview)));
            environmentVariables.Add("Controllarr_Release_Title", remoteGame.Release.Title);
            environmentVariables.Add("Controllarr_Release_Indexer", remoteGame.Release.Indexer ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Size", remoteGame.Release.Size.ToString());
            environmentVariables.Add("Controllarr_Release_Quality", remoteGame.ParsedEpisodeInfo.Quality.Quality.Name);
            environmentVariables.Add("Controllarr_Release_QualityVersion", remoteGame.ParsedEpisodeInfo.Quality.Revision.Version.ToString());
            environmentVariables.Add("Controllarr_Release_ReleaseGroup", releaseGroup ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_IndexerFlags", remoteGame.Release.IndexerFlags.ToString());
            environmentVariables.Add("Controllarr_Download_Client", message.DownloadClientName ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_CustomFormat", string.Join("|", remoteGame.CustomFormats));
            environmentVariables.Add("Controllarr_Release_CustomFormatScore", remoteGame.CustomFormatScore.ToString());
            environmentVariables.Add("Controllarr_Release_ReleaseType", remoteGame.ParsedEpisodeInfo.ReleaseType.ToString());

            ExecuteScript(environmentVariables);
        }

        public override void OnDownload(DownloadMessage message)
        {
            var platform = message.Series;
            var romFile = message.EpisodeFile;
            var sourcePath = message.SourcePath;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Download");
            AddPlatformVariables(environmentVariables, platform);

            environmentVariables.Add("Controllarr_IsUpgrade", message.OldFiles.Any().ToString());
            environmentVariables.Add("Controllarr_RomFile_Id", romFile.Id.ToString());
            environmentVariables.Add("Controllarr_RomFile_GameCount", romFile.Episodes.Value.Count.ToString());
            environmentVariables.Add("Controllarr_RomFile_RelativePath", romFile.RelativePath);
            environmentVariables.Add("Controllarr_RomFile_Path", Path.Combine(platform.Path, romFile.RelativePath));
            environmentVariables.Add("Controllarr_RomFile_GameIds", string.Join(",", romFile.Episodes.Value.Select(e => e.Id)));
            environmentVariables.Add("Controllarr_RomFile_RegionNumber", romFile.SeasonNumber.ToString());
            environmentVariables.Add("Controllarr_RomFile_Region", RegionHelper.GetRegionName(romFile.SeasonNumber));
            environmentVariables.Add("Controllarr_RomFile_GameNumbers", string.Join(",", romFile.Episodes.Value.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Controllarr_RomFile_GameTitles", string.Join("|", romFile.Episodes.Value.Select(e => e.Title)));
            environmentVariables.Add("Controllarr_RomFile_GameOverviews", string.Join("|", romFile.Episodes.Value.Select(e => e.Overview)));
            environmentVariables.Add("Controllarr_RomFile_Quality", romFile.Quality.Quality.Name);
            environmentVariables.Add("Controllarr_RomFile_QualityVersion", romFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Controllarr_RomFile_ReleaseGroup", romFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Controllarr_RomFile_SceneName", romFile.SceneName ?? string.Empty);
            environmentVariables.Add("Controllarr_RomFile_SourcePath", sourcePath);
            environmentVariables.Add("Controllarr_RomFile_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Controllarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Controllarr_RomFile_CustomFormat", string.Join("|", message.EpisodeInfo.CustomFormats));
            environmentVariables.Add("Controllarr_RomFile_CustomFormatScore", message.EpisodeInfo.CustomFormatScore.ToString());
            environmentVariables.Add("Controllarr_Release_Indexer", message.Release?.Indexer ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Size", message.Release?.Size.ToString() ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Title", message.Release?.Title ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_ReleaseType", message.Release?.ReleaseType.ToString() ?? string.Empty);

            if (message.OldFiles.Any())
            {
                environmentVariables.Add("Controllarr_DeletedRelativePaths", string.Join("|", message.OldFiles.Select(e => e.EpisodeFile.RelativePath)));
                environmentVariables.Add("Controllarr_DeletedPaths", string.Join("|", message.OldFiles.Select(e => Path.Combine(platform.Path, e.EpisodeFile.RelativePath))));
                environmentVariables.Add("Controllarr_DeletedDateAdded", string.Join("|", message.OldFiles.Select(e => e.EpisodeFile.DateAdded)));
                environmentVariables.Add("Controllarr_DeletedRecycleBinPaths", string.Join("|", message.OldFiles.Select(e => e.RecycleBinPath ?? string.Empty)));
            }

            ExecuteScript(environmentVariables);
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            var platform = message.Series;
            var games = message.Episodes;
            var romFiles = message.EpisodeFiles;
            var sourcePath = message.SourcePath;
            var regionNumber = games.FirstOrDefault()?.SeasonNumber ?? 0;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Download");
            AddPlatformVariables(environmentVariables, platform);

            environmentVariables.Add("Controllarr_RomFile_Ids", string.Join("|", romFiles.Select(f => f.Id)));
            environmentVariables.Add("Controllarr_RomFile_Count", message.EpisodeFiles.Count.ToString());
            environmentVariables.Add("Controllarr_RomFile_RelativePaths", string.Join("|", romFiles.Select(f => f.RelativePath)));
            environmentVariables.Add("Controllarr_RomFile_Paths", string.Join("|", romFiles.Select(f => Path.Combine(platform.Path, f.RelativePath))));
            environmentVariables.Add("Controllarr_RomFile_GameIds", string.Join(",", games.Select(e => e.Id)));
            environmentVariables.Add("Controllarr_RomFile_RegionNumber", regionNumber.ToString());
            environmentVariables.Add("Controllarr_RomFile_Region", RegionHelper.GetRegionName(regionNumber));
            environmentVariables.Add("Controllarr_RomFile_GameNumbers", string.Join(",", games.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Controllarr_RomFile_GameTitles", string.Join("|", games.Select(e => e.Title)));
            environmentVariables.Add("Controllarr_RomFile_GameOverviews", string.Join("|", games.Select(e => e.Overview)));
            environmentVariables.Add("Controllarr_RomFile_Qualities", string.Join("|", romFiles.Select(f => f.Quality.Quality.Name)));
            environmentVariables.Add("Controllarr_RomFile_QualityVersions", string.Join("|", romFiles.Select(f => f.Quality.Revision.Version)));
            environmentVariables.Add("Controllarr_RomFile_ReleaseGroups", string.Join("|", romFiles.Select(f => f.ReleaseGroup)));
            environmentVariables.Add("Controllarr_RomFile_SceneNames", string.Join("|", romFiles.Select(f => f.SceneName)));
            environmentVariables.Add("Controllarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Group", message.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Quality", message.ReleaseQuality.Quality.Name);
            environmentVariables.Add("Controllarr_Release_QualityVersion", message.ReleaseQuality.Revision.Version.ToString());
            environmentVariables.Add("Controllarr_Release_Indexer", message.Release?.Indexer ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Size", message.Release?.Size.ToString() ?? string.Empty);
            environmentVariables.Add("Controllarr_Release_Title", message.Release?.Title ?? string.Empty);

            // Prefer the release type from the release, otherwise use the first imported file (useful for untracked manual imports)
            environmentVariables.Add("Controllarr_Release_ReleaseType", message.Release == null ? message.EpisodeFiles.First().ReleaseType.ToString() : message.Release.ReleaseType.ToString());
            environmentVariables.Add("Controllarr_SourcePath", sourcePath);
            environmentVariables.Add("Controllarr_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Controllarr_DestinationPath", message.DestinationPath);
            environmentVariables.Add("Controllarr_DestinationFolder", Path.GetDirectoryName(message.DestinationPath));

            ExecuteScript(environmentVariables);
        }

        public override void OnRename(Series series, List<RenamedRomFile> renamedFiles)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Rename");
            AddPlatformVariables(environmentVariables, series);

            environmentVariables.Add("Controllarr_RomFile_Ids", string.Join(",", renamedFiles.Select(e => e.EpisodeFile.Id)));
            environmentVariables.Add("Controllarr_RomFile_RelativePaths", string.Join("|", renamedFiles.Select(e => e.EpisodeFile.RelativePath)));
            environmentVariables.Add("Controllarr_RomFile_Paths", string.Join("|", renamedFiles.Select(e => Path.Combine(series.Path, e.EpisodeFile.RelativePath))));
            environmentVariables.Add("Controllarr_RomFile_PreviousRelativePaths", string.Join("|", renamedFiles.Select(e => e.PreviousRelativePath)));
            environmentVariables.Add("Controllarr_RomFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            ExecuteScript(environmentVariables);
        }

        public override void OnEpisodeFileDelete(GameDeleteMessage deleteMessage)
        {
            var platform = deleteMessage.Series;
            var romFile = deleteMessage.EpisodeFile;

            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "RomFileDelete");
            AddPlatformVariables(environmentVariables, platform);

            environmentVariables.Add("Controllarr_RomFile_Id", romFile.Id.ToString());
            environmentVariables.Add("Controllarr_RomFile_GameCount", romFile.Episodes.Value.Count.ToString());
            environmentVariables.Add("Controllarr_RomFile_RelativePath", romFile.RelativePath);
            environmentVariables.Add("Controllarr_RomFile_Path", Path.Combine(platform.Path, romFile.RelativePath));
            environmentVariables.Add("Controllarr_RomFile_GameIds", string.Join(",", romFile.Episodes.Value.Select(e => e.Id)));
            environmentVariables.Add("Controllarr_RomFile_RegionNumber", romFile.SeasonNumber.ToString());
            environmentVariables.Add("Controllarr_RomFile_Region", RegionHelper.GetRegionName(romFile.SeasonNumber));
            environmentVariables.Add("Controllarr_RomFile_GameNumbers", string.Join(",", romFile.Episodes.Value.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Controllarr_RomFile_GameTitles", string.Join("|", romFile.Episodes.Value.Select(e => e.Title)));
            environmentVariables.Add("Controllarr_RomFile_GameOverviews", string.Join("|", romFile.Episodes.Value.Select(e => e.Overview)));
            environmentVariables.Add("Controllarr_RomFile_Quality", romFile.Quality.Quality.Name);
            environmentVariables.Add("Controllarr_RomFile_QualityVersion", romFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Controllarr_RomFile_ReleaseGroup", romFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Controllarr_RomFile_SceneName", romFile.SceneName ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnSeriesAdd(PlatformAddMessage message)
        {
            var platform = message.Series;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "PlatformAdd");
            AddPlatformVariables(environmentVariables, platform);

            ExecuteScript(environmentVariables);
        }

        public override void OnSeriesDelete(PlatformDeleteMessage deleteMessage)
        {
            var platform = deleteMessage.Series;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "PlatformDelete");
            AddPlatformVariables(environmentVariables, platform);

            ExecuteScript(environmentVariables);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "HealthIssue");

            environmentVariables.Add("Controllarr_Health_Issue_Level", Enum.GetName(typeof(HealthCheckResult), healthCheck.Type));
            environmentVariables.Add("Controllarr_Health_Issue_Message", healthCheck.Message);
            environmentVariables.Add("Controllarr_Health_Issue_Type", healthCheck.Source.Name);
            environmentVariables.Add("Controllarr_Health_Issue_Wiki", healthCheck.WikiUrl?.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "HealthRestored");

            environmentVariables.Add("Controllarr_Health_Restored_Level", Enum.GetName(typeof(HealthCheckResult), previousCheck.Type));
            environmentVariables.Add("Controllarr_Health_Restored_Message", previousCheck.Message);
            environmentVariables.Add("Controllarr_Health_Restored_Type", previousCheck.Source.Name);
            environmentVariables.Add("Controllarr_Health_Restored_Wiki", previousCheck.WikiUrl?.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "ApplicationUpdate");

            environmentVariables.Add("Controllarr_Update_Message", updateMessage.Message);
            environmentVariables.Add("Controllarr_Update_NewVersion", updateMessage.NewVersion.ToString());
            environmentVariables.Add("Controllarr_Update_PreviousVersion", updateMessage.PreviousVersion.ToString());

            ExecuteScript(environmentVariables);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            var platform = message.Series;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "ManualInteractionRequired");
            AddPlatformVariables(environmentVariables, platform);

            environmentVariables.Add("Controllarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Size", message.TrackedDownload.DownloadItem?.TotalSize.ToString() ?? string.Empty);
            environmentVariables.Add("Controllarr_Download_Title", message.TrackedDownload.DownloadItem?.Title ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            if (!_diskProvider.FileExists(Settings.Path))
            {
                failures.Add(new NzbDroneValidationFailure("Path", _localizationService.GetLocalizedString("NotificationsCustomScriptValidationFileDoesNotExist")));
            }

            if (failures.Empty())
            {
                try
                {
                    var environmentVariables = new StringDictionary();
                    environmentVariables.Add("Controllarr_EventType", "Test");
                    environmentVariables.Add("Controllarr_InstanceName", _configFileProvider.InstanceName);
                    environmentVariables.Add("Controllarr_ApplicationUrl", _configService.ApplicationUrl);

                    var processOutput = ExecuteScript(environmentVariables);

                    if (processOutput.ExitCode != 0)
                    {
                        failures.Add(new NzbDroneValidationFailure(string.Empty, $"Script exited with code: {processOutput.ExitCode}"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    failures.Add(new NzbDroneValidationFailure(string.Empty, ex.Message));
                }
            }

            return new ValidationResult(failures);
        }

        private ProcessOutput ExecuteScript(StringDictionary environmentVariables)
        {
            _logger.Debug("Executing external script: {0}", Settings.Path);

            var processOutput = _processProvider.StartAndCapture(Settings.Path, Settings.Arguments, environmentVariables);

            _logger.Debug("Executed external script: {0} - Status: {1}", Settings.Path, processOutput.ExitCode);
            _logger.Debug("Script Output: \r\n{0}", string.Join("\r\n", processOutput.Lines));

            return processOutput;
        }

        private bool ValidatePathParent(string possibleParent, string path)
        {
            return possibleParent.IsParentPath(path);
        }

        private List<string> GetTagLabels(Series series)
        {
            if (series == null)
            {
                return new List<string>();
            }

            return _tagRepository.GetTags(series.Tags)
                .Select(s => s.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }

        private void AddInstanceVariables(StringDictionary environmentVariables, string eventType)
        {
            environmentVariables.Add("Controllarr_EventType", eventType);
            environmentVariables.Add("Controllarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Controllarr_ApplicationUrl", _configService.ApplicationUrl);
        }

        private void AddPlatformVariables(StringDictionary environmentVariables, Series platform)
        {
            if (platform == null)
            {
                return;
            }

            environmentVariables.Add("Controllarr_Platform_Id", platform.Id.ToString());
            environmentVariables.Add("Controllarr_Platform_Title", platform.Title);
            environmentVariables.Add("Controllarr_Platform_TitleSlug", platform.TitleSlug);
            environmentVariables.Add("Controllarr_Platform_Path", platform.Path);
            environmentVariables.Add("Controllarr_Platform_CatalogId", platform.TvdbId.ToString());
            environmentVariables.Add("Controllarr_Platform_Year", platform.Year.ToString());
            environmentVariables.Add("Controllarr_Platform_Genres", string.Join("|", platform.Genres));
            environmentVariables.Add("Controllarr_Platform_Tags", string.Join("|", GetTagLabels(platform)));
        }
    }
}
