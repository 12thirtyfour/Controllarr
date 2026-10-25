import React from 'react';
import { Navigate, Route } from 'react-router-dom';
import Blocklist from 'Activity/Blocklist/Blocklist';
import History from 'Activity/History/History';
import Queue from 'Activity/Queue/Queue';
import AddNewPlatform from 'AddPlatform/AddNewPlatform/AddNewPlatform';
import ImportPlatform from 'AddPlatform/ImportPlatform/Import/ImportPlatform';
import ImportPlatformSelectFolder from 'AddPlatform/ImportPlatform/SelectFolder/ImportPlatformSelectFolder';
import CalendarPage from 'Calendar/CalendarPage';
import NotFound from 'Components/NotFound';
import PlatformDetailsPage from 'Platform/Details/PlatformDetailsPage';
import PlatformIndex from 'Platform/Index/PlatformIndex';
import BiosSettings from 'Settings/Bios/BiosSettings';
import CollectionSettings from 'Settings/Collection/CollectionSettings';
import CustomFormatSettingsPage from 'Settings/CustomFormats/CustomFormatSettingsPage';
import DownloadClientSettings from 'Settings/DownloadClients/DownloadClientSettings';
import GeneralSettings from 'Settings/General/GeneralSettings';
import ImportListSettings from 'Settings/ImportLists/ImportListSettings';
import IndexerSettings from 'Settings/Indexers/IndexerSettings';
import LibretroSettings from 'Settings/Libretro/LibretroSettings';
import MediaManagement from 'Settings/MediaManagement/MediaManagement';
import MetadataSettings from 'Settings/Metadata/MetadataSettings';
import NotificationSettings from 'Settings/Notifications/NotificationSettings';
import Profiles from 'Settings/Profiles/Profiles';
import Quality from 'Settings/Quality/Quality';
import RetroAchievementsSettings from 'Settings/RetroAchievements/RetroAchievementsSettings';
import Settings from 'Settings/Settings';
import TagSettings from 'Settings/Tags/TagSettings';
import UISettings from 'Settings/UI/UISettings';
import Statistics from 'Statistics/Statistics';
import Backups from 'System/Backup/Backups';
import LogsTable from 'System/Events/LogsTable';
import AppLogFiles from 'System/Logs/App/AppLogFiles';
import UpdateLogFiles from 'System/Logs/Update/UpdateLogFiles';
import Status from 'System/Status/Status';
import Tasks from 'System/Tasks/Tasks';
import Updates from 'System/Updates/Updates';
import CutoffUnmet from 'Wanted/CutoffUnmet/CutoffUnmet';
import Missing from 'Wanted/Missing/Missing';
import WebPlayerPage from 'WebPlayer/WebPlayerPage';

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
        Calendar
      */}

      <Route path="/calendar" element={<CalendarPage />} />

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

      <Route path="/settings/profiles" element={<Profiles />} />

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
