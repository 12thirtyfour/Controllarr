import React, { lazy } from 'react';
import { Navigate, Route } from 'react-router-dom';
import NotFound from 'Components/NotFound';
import PlatformDetailsPage from 'Platform/Details/PlatformDetailsPage';
import PlatformIndex from 'Platform/Index/PlatformIndex';

const Blocklist = lazy(() => import('Activity/Blocklist/Blocklist'));
const History = lazy(() => import('Activity/History/History'));
const Queue = lazy(() => import('Activity/Queue/Queue'));
const AddNewPlatform = lazy(() => import('AddPlatform/AddNewPlatform/AddNewPlatform'));
const ImportPlatform = lazy(() => import('AddPlatform/ImportPlatform/Import/ImportPlatform'));
const ImportPlatformSelectFolder = lazy(
  () => import('AddPlatform/ImportPlatform/SelectFolder/ImportPlatformSelectFolder')
);
const RomPatcherPage = lazy(() => import('RomPatcher/RomPatcherPage'));
const BiosSettings = lazy(() => import('Settings/Bios/BiosSettings'));
const CollectionSettings = lazy(() => import('Settings/Collection/CollectionSettings'));
const CustomFormatSettingsPage = lazy(
  () => import('Settings/CustomFormats/CustomFormatSettingsPage')
);
const DownloadClientSettings = lazy(
  () => import('Settings/DownloadClients/DownloadClientSettings')
);
const GeneralSettings = lazy(() => import('Settings/General/GeneralSettings'));
const ImportListSettings = lazy(() => import('Settings/ImportLists/ImportListSettings'));
const IndexerSettings = lazy(() => import('Settings/Indexers/IndexerSettings'));
const LibretroSettings = lazy(() => import('Settings/Libretro/LibretroSettings'));
const MediaManagement = lazy(() => import('Settings/MediaManagement/MediaManagement'));
const MetadataSettings = lazy(() => import('Settings/Metadata/MetadataSettings'));
const NotificationSettings = lazy(
  () => import('Settings/Notifications/NotificationSettings')
);
const Profiles = lazy(() => import('Settings/Profiles/Profiles'));
const Quality = lazy(() => import('Settings/Quality/Quality'));
const RetroAchievementsSettings = lazy(
  () => import('Settings/RetroAchievements/RetroAchievementsSettings')
);
const Settings = lazy(() => import('Settings/Settings'));
const TagSettings = lazy(() => import('Settings/Tags/TagSettings'));
const UISettings = lazy(() => import('Settings/UI/UISettings'));
const Statistics = lazy(() => import('Statistics/Statistics'));
const Backups = lazy(() => import('System/Backup/Backups'));
const LogsTable = lazy(() => import('System/Events/LogsTable'));
const AppLogFiles = lazy(() => import('System/Logs/App/AppLogFiles'));
const UpdateLogFiles = lazy(() => import('System/Logs/Update/UpdateLogFiles'));
const Status = lazy(() => import('System/Status/Status'));
const Tasks = lazy(() => import('System/Tasks/Tasks'));
const Updates = lazy(() => import('System/Updates/Updates'));
const CutoffUnmet = lazy(() => import('Wanted/CutoffUnmet/CutoffUnmet'));
const Missing = lazy(() => import('Wanted/Missing/Missing'));
const WebPlayerPage = lazy(() => import('WebPlayer/WebPlayerPage'));

export function appRouteElements() {
  return (
    <>
      {/*
        Home (v5) — redirects to PlatformIndex
      */}

      <Route path="/" element={<Navigate to="/platforms" replace={true} />} />

      {/*
        Platforms
      */}

      <Route path="/platforms" element={<PlatformIndex />} />
      <Route path="/platforms/:titleSlug" element={<PlatformDetailsPage />} />
      <Route
        path="/series"
        element={<Navigate to="/platforms" replace={true} />}
      />

      <Route path="/add/new" element={<AddNewPlatform />} />

      <Route path="/add/import" element={<ImportPlatformSelectFolder />} />

      <Route path="/add/import/:rootFolderId" element={<ImportPlatform />} />

      <Route path="/bios" element={<BiosSettings />} />

      <Route path="/rom-patcher" element={<RomPatcherPage />} />

      <Route path="/webplayer" element={<WebPlayerPage />} />

      <Route
        path="/serieseditor"
        element={<Navigate to="/platforms" replace={true} />}
      />

      <Route
        path="/seasonpass"
        element={<Navigate to="/platforms" replace={true} />}
      />

      <Route path="/series/:titleSlug" element={<PlatformDetailsPage />} />

      <Route path="/statistics" element={<Statistics />} />

      {/*
        Activity
      */}

      <Route path="/activity/history" element={<History />} />

      <Route path="/activity/queue" element={<Queue />} />

      <Route path="/activity/blocklist" element={<Blocklist />} />

      {/*
        Wanted
      */}

      <Route path="/wanted/missing" element={<Missing />} />

      <Route path="/wanted/cutoffunmet" element={<CutoffUnmet />} />

      {/*
        Settings
      */}

      <Route path="/settings" element={<Settings />} />

      <Route path="/settings/general" element={<GeneralSettings />} />

      <Route path="/settings/ui" element={<UISettings />} />

      <Route path="/settings/mediamanagement" element={<MediaManagement />} />

      <Route path="/settings/collection" element={<CollectionSettings />} />

      <Route path="/settings/virtuallibraries" element={<Profiles />} />
      <Route
        path="/settings/profiles"
        element={<Navigate to="/settings/virtuallibraries" replace={true} />}
      />

      <Route path="/settings/indexers" element={<IndexerSettings />} />

      <Route
        path="/settings/downloadclients"
        element={<DownloadClientSettings />}
      />

      <Route path="/settings/metadata" element={<MetadataSettings />} />

      <Route
        path="/settings/metadatasource"
        element={<Navigate to="/settings/metadata" replace={true} />}
      />

      <Route path="/settings/connect" element={<NotificationSettings />} />

      <Route path="/settings/importlists" element={<ImportListSettings />} />

      <Route path="/settings/tags" element={<TagSettings />} />

      <Route
        path="/settings/retroachievements"
        element={<RetroAchievementsSettings />}
      />

      <Route path="/settings/libretro" element={<LibretroSettings />} />

      <Route path="/settings/quality" element={<Quality />} />

      <Route
        path="/settings/customformats"
        element={<CustomFormatSettingsPage />}
      />

      <Route
        path="/settings/bios"
        element={<Navigate to="/bios" replace={true} />}
      />

      {/*
        System
      */}

      <Route path="/system/status" element={<Status />} />

      <Route path="/system/tasks" element={<Tasks />} />

      <Route path="/system/backups" element={<Backups />} />

      <Route path="/system/updates" element={<Updates />} />

      <Route path="/system/events" element={<LogsTable />} />

      <Route path="/system/logs/files" element={<AppLogFiles />} />

      <Route path="/system/logs/files/update" element={<UpdateLogFiles />} />

      {/*
        Not Found
      */}

      <Route path="*" element={<NotFound />} />
    </>
  );
}
