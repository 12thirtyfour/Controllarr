using System.Collections.Generic;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Security;

namespace NzbDrone.Core.Configuration
{
    public interface IConfigService
    {
        void SaveConfigDictionary(Dictionary<string, object> configValues);

        bool IsDefined(string key);

        // Download Client
        string DownloadClientWorkingFolders { get; set; }
        int DownloadClientHistoryLimit { get; set; }

        // Completed/Failed Download Handling (Download client)
        bool EnableCompletedDownloadHandling { get; set; }
        bool AutoRedownloadFailed { get; set; }
        bool AutoRedownloadFailedFromInteractiveSearch { get; set; }

        // Media Management
        bool AutoUnmonitorPreviouslyDownloadedEpisodes { get; set; }
        string RecycleBin { get; set; }
        int RecycleBinCleanupDays { get; set; }
        ProperDownloadTypes DownloadPropersAndRepacks { get; set; }
        bool CreateEmptySeriesFolders { get; set; }
        bool DeleteEmptyFolders { get; set; }
        FileDateType FileDate { get; set; }
        bool SkipFreeSpaceCheckWhenGrabbing { get; set; }
        bool SkipFreeSpaceCheckWhenImporting { get; set; }
        int MinimumFreeSpaceWhenImporting { get; set; }
        bool CopyUsingHardlinks { get; set; }
        bool EnableMediaInfo { get; set; }
        bool UseScriptImport { get; set; }
        string ScriptImportPath { get; set; }
        bool ImportExtraFiles { get; set; }
        string ExtraFileExtensions { get; set; }
        RescanAfterRefreshType RescanAfterRefresh { get; set; }
        EpisodeTitleRequiredType EpisodeTitleRequired { get; set; }
        string UserRejectedExtensions { get; set; }

        // Season Pack Upgrade (Media Management)
        SeasonPackUpgradeType SeasonPackUpgrade { get; set; }
        double SeasonPackUpgradeThreshold { get; set; }

        bool ExtractFullSetCompletely { get; set; }
        bool AutoCompressDiscsToChd { get; set; }
        bool DeleteOriginalAfterConversion { get; set; }
        bool RequireUserConfirmationForBatchConversions { get; set; }

        // Permissions (Media Management)
        bool SetPermissionsLinux { get; set; }
        string ChmodFolder { get; set; }
        string ChownGroup { get; set; }

        // Indexers
        int Retention { get; set; }
        int RssSyncInterval { get; set; }
        int MaximumSize { get; set; }
        int MinimumAge { get; set; }

        ListSyncLevelType ListSyncLevel { get; set; }
        int ListSyncTag { get; set; }

        // UI
        int FirstDayOfWeek { get; set; }
        string CalendarWeekColumnHeader { get; set; }

        string ShortDateFormat { get; set; }
        string LongDateFormat { get; set; }
        string TimeFormat { get; set; }
        string TimeZone { get; set; }
        bool ShowRelativeDates { get; set; }
        bool EnableColorImpairedMode { get; set; }
        int UILanguage { get; set; }
        int PreferredMetadataLanguage { get; set; }

        // Internal
        bool CleanupMetadataImages { get; set; }
        string PlexClientIdentifier { get; }

        // Forms Auth
        string RijndaelPassphrase { get; }
        string HmacPassphrase { get; }
        string RijndaelSalt { get; }
        string HmacSalt { get; }

        // Proxy
        bool ProxyEnabled { get; }
        ProxyType ProxyType { get; }
        string ProxyHostname { get; }
        int ProxyPort { get; }
        string ProxyUsername { get; }
        string ProxyPassword { get; }
        string ProxyBypassFilter { get; }
        bool ProxyBypassLocalAddresses { get; }

        // Backups
        string BackupFolder { get; }
        int BackupInterval { get; }
        int BackupRetention { get; }

        CertificateValidationType CertificateValidation { get; }
        string ApplicationUrl { get; }

        // IGDB
        string IgdbClientId { get; set; }
        string IgdbClientSecret { get; set; }
        bool EnableNoIntroLibretro { get; set; }
        bool EnableHtgdbSmdb { get; set; }
        bool EnableLaunchBox { get; set; }
        bool EnableIgdb { get; set; }
        bool EnableScreenScraper { get; set; }
        string ScreenScraperUsername { get; set; }
        string ScreenScraperPassword { get; set; }
        bool EnableTheGamesDb { get; set; }
        string TheGamesDbApiKey { get; set; }
        bool MetadataBoxArt2d { get; set; }
        bool MetadataBoxArt3d { get; set; }
        bool MetadataTitleScreens { get; set; }
        bool MetadataScreenshots { get; set; }
        bool MetadataClearLogos { get; set; }
        bool MetadataMediaScans { get; set; }
        bool MetadataVideoPreviews { get; set; }
        bool MetadataManualsPdf { get; set; }
        bool MetadataPublishingMetadata { get; set; }
        bool MetadataGenerateGamelistXml { get; set; }
    }
}
