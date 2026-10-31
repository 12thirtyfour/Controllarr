using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Qualities;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class MediaManagementConfigResource : RestResource
    {
        private bool _autoUnmonitorPreviouslyDownloadedEpisodes;
        private EpisodeTitleRequiredType _episodeTitleRequired;

        public bool AutoUnmonitorPreviouslyDownloadedEpisodes
        {
            get => _autoUnmonitorPreviouslyDownloadedEpisodes;
            set => _autoUnmonitorPreviouslyDownloadedEpisodes = value;
        }

        public bool AutoUnmonitorPreviouslyDownloadedGames
        {
            get => _autoUnmonitorPreviouslyDownloadedEpisodes;
            set => _autoUnmonitorPreviouslyDownloadedEpisodes = value;
        }

        public string RecycleBin { get; set; }

        public int RecycleBinCleanupDays { get; set; }

        public ProperDownloadTypes DownloadPropersAndRepacks { get; set; }

        public bool CreateEmptySeriesFolders { get; set; }

        public bool DeleteEmptyFolders { get; set; }

        public FileDateType FileDate { get; set; }

        public RescanAfterRefreshType RescanAfterRefresh { get; set; }

        public bool SetPermissionsLinux { get; set; }

        public string ChmodFolder { get; set; }

        public string ChownGroup { get; set; }

        public EpisodeTitleRequiredType EpisodeTitleRequired
        {
            get => _episodeTitleRequired;
            set => _episodeTitleRequired = value;
        }

        public EpisodeTitleRequiredType GameTitleRequired
        {
            get => _episodeTitleRequired;
            set => _episodeTitleRequired = value;
        }

        public bool SkipFreeSpaceCheckWhenGrabbing { get; set; }

        public bool SkipFreeSpaceCheckWhenImporting { get; set; }

        public int MinimumFreeSpaceWhenImporting { get; set; }

        public bool CopyUsingHardlinks { get; set; }

        public bool UseScriptImport { get; set; }

        public string ScriptImportPath { get; set; }

        public bool ImportExtraFiles { get; set; }

        public string ExtraFileExtensions { get; set; }

        public bool EnableMediaInfo { get; set; }

        public string UserRejectedExtensions { get; set; }

        public SeasonPackUpgradeType SeasonPackUpgrade { get; set; }

        public double SeasonPackUpgradeThreshold { get; set; }
    }

    public static class MediaManagementConfigResourceMapper
    {
        public static MediaManagementConfigResource ToResource(IConfigService model)
        {
            return new MediaManagementConfigResource
            {
                AutoUnmonitorPreviouslyDownloadedEpisodes = model.AutoUnmonitorPreviouslyDownloadedGames,
                AutoUnmonitorPreviouslyDownloadedGames = model.AutoUnmonitorPreviouslyDownloadedGames,
                RecycleBin = model.RecycleBin,
                RecycleBinCleanupDays = model.RecycleBinCleanupDays,
                DownloadPropersAndRepacks = model.DownloadPropersAndRepacks,
                CreateEmptySeriesFolders = model.CreateEmptySeriesFolders,
                DeleteEmptyFolders = model.DeleteEmptyFolders,
                FileDate = model.FileDate,
                RescanAfterRefresh = model.RescanAfterRefresh,

                SetPermissionsLinux = model.SetPermissionsLinux,
                ChmodFolder = model.ChmodFolder,
                ChownGroup = model.ChownGroup,

                EpisodeTitleRequired = model.GameTitleRequired,
                GameTitleRequired = model.GameTitleRequired,
                SkipFreeSpaceCheckWhenImporting = model.SkipFreeSpaceCheckWhenImporting,
                SkipFreeSpaceCheckWhenGrabbing = model.SkipFreeSpaceCheckWhenGrabbing,
                MinimumFreeSpaceWhenImporting = model.MinimumFreeSpaceWhenImporting,
                CopyUsingHardlinks = model.CopyUsingHardlinks,
                UseScriptImport = model.UseScriptImport,
                ScriptImportPath = model.ScriptImportPath,
                ImportExtraFiles = model.ImportExtraFiles,
                ExtraFileExtensions = model.ExtraFileExtensions,
                EnableMediaInfo = model.EnableMediaInfo,
                UserRejectedExtensions = model.UserRejectedExtensions,
                SeasonPackUpgrade = model.SeasonPackUpgrade,
                SeasonPackUpgradeThreshold = model.SeasonPackUpgradeThreshold
            };
        }
    }
}
