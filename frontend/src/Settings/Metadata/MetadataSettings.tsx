import React, { useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

interface DatFileRecord {
  id: number;
  name: string;
  description?: string;
  version?: string;
  author?: string;
  category?: string;
}

interface DatImportRequest {
  filePath?: string;
  fileName?: string;
  content?: string;
  format?: string;
}

interface DatImportResult {
  datFile?: DatFileRecord;
  entryCount: number;
}

interface DatCatalogSyncResult {
  platformsSynced: number;
  totalEntriesSynced: number;
  syncedPlatformNames?: string[];
}

interface RomPatchRequest {
  seriesId?: number;
  baseRomPath?: string;
  patchFilePath?: string;
  patchFileName?: string;
  patchBase64Content?: string;
  outputTitle?: string;
}

interface RomPatchResult {
  success: boolean;
  message: string;
  format?: string;
  baseRomPath?: string;
  outputRomPath?: string;
  outputCrc32?: string;
  outputSha1?: string;
  outputSha256?: string;
  verifiedInCatalog?: boolean;
  matchedCatalogName?: string;
}

interface MetadataScraperConfig {
  id: string;
  name: string;
  description: string;
  requiresCredentials: boolean;
  enabled: boolean;
  priority: number;
  maxRequestsPerMinute: number;
  username: string;
  password: string;
  clientId: string;
  clientSecret: string;
  apiKey: string;
  publishingMetadata: boolean;
  boxArt2d: boolean;
  boxArt3d: boolean;
  screenshots: boolean;
  titleScreens: boolean;
  clearLogos: boolean;
  videoPreviews: boolean;
  manualsPdf: boolean;
}

interface UnifiedMetadataSettings {
  generateGamelistXml: boolean;
  scrapers: MetadataScraperConfig[];
}

const STORAGE_KEY = 'controllarr_unified_metadata_settings_v2';

const DEFAULT_SCRAPERS: MetadataScraperConfig[] = [
  {
    id: 'screenscraper',
    name: 'ScreenScraper.fr',
    description:
      'Comprehensive retro artwork, 3D box renders, clear logos, gameplay video snaps (.mp4), and PDF manuals',
    requiresCredentials: true,
    enabled: false,
    priority: 1,
    maxRequestsPerMinute: 30,
    username: '',
    password: '',
    clientId: '',
    clientSecret: '',
    apiKey: '',
    publishingMetadata: true,
    boxArt2d: true,
    boxArt3d: true,
    screenshots: true,
    titleScreens: true,
    clearLogos: true,
    videoPreviews: true,
    manualsPdf: true,
  },
  {
    id: 'igdb',
    name: 'IGDB (Twitch Developer API)',
    description:
      'Rich game synopses, release dates, developer/publisher credits, cover art, and screenshots',
    requiresCredentials: true,
    enabled: false,
    priority: 2,
    maxRequestsPerMinute: 120,
    username: '',
    password: '',
    clientId: '',
    clientSecret: '',
    apiKey: '',
    publishingMetadata: true,
    boxArt2d: true,
    boxArt3d: false,
    screenshots: true,
    titleScreens: true,
    clearLogos: false,
    videoPreviews: false,
    manualsPdf: false,
  },
  {
    id: 'thegamesdb',
    name: 'TheGamesDB (TGDB)',
    description:
      'Community console box art, clear logos, screenshots, and release information',
    requiresCredentials: true,
    enabled: false,
    priority: 3,
    maxRequestsPerMinute: 60,
    username: '',
    password: '',
    clientId: '',
    clientSecret: '',
    apiKey: '',
    publishingMetadata: true,
    boxArt2d: true,
    boxArt3d: false,
    screenshots: true,
    titleScreens: true,
    clearLogos: true,
    videoPreviews: false,
    manualsPdf: false,
  },
  {
    id: 'libretro_launchbox',
    name: 'Libretro Thumbnails & LaunchBox Community DB',
    description:
      'Public CDN for 2D box art, in-game snaps, title screens, clear logos, and release years (no account required)',
    requiresCredentials: false,
    enabled: true,
    priority: 4,
    maxRequestsPerMinute: 180,
    username: '',
    password: '',
    clientId: '',
    clientSecret: '',
    apiKey: '',
    publishingMetadata: true,
    boxArt2d: true,
    boxArt3d: false,
    screenshots: true,
    titleScreens: true,
    clearLogos: true,
    videoPreviews: false,
    manualsPdf: false,
  },
];

const DEFAULT_SETTINGS: UnifiedMetadataSettings = {
  generateGamelistXml: true,
  scrapers: DEFAULT_SCRAPERS,
};

function loadInitialSettings(): UnifiedMetadataSettings {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);

    if (raw) {
      const parsed = JSON.parse(raw) as Partial<UnifiedMetadataSettings>;
      if (parsed && Array.isArray(parsed.scrapers) && parsed.scrapers.length > 0) {
        return {
          generateGamelistXml:
            parsed.generateGamelistXml ?? DEFAULT_SETTINGS.generateGamelistXml,
          scrapers: parsed.scrapers,
        };
      }
    }
  } catch {
    // ignore storage errors
  }

  return DEFAULT_SETTINGS;
}

function MetadataSettings() {
  const [settings, setSettings] =
    useState<UnifiedMetadataSettings>(loadInitialSettings);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);
  const [datServerPath, setDatServerPath] = useState('');
  const [datImportStatus, setDatImportStatus] = useState<string | null>(null);
  const [patchServerPath, setPatchServerPath] = useState('');
  const [baseRomServerPath, setBaseRomServerPath] = useState('');
  const [patchOutputTitle, setPatchOutputTitle] = useState('');
  const [patchStatus, setPatchStatus] = useState<RomPatchResult | null>(null);

  const { data: backendSettings } = useApiQuery<UnifiedMetadataSettings>({
    path: '/gamemetadata/settings',
  });

  const { data: importedDats, refetch: refetchDats } = useApiQuery<
    DatFileRecord[]
  >({
    path: '/gamemetadata/dats',
  });

  const { mutate: syncOnlineDats, isPending: isSyncingOnlineDats } =
    useApiMutation<DatCatalogSyncResult, Record<string, never>>({
      path: '/gamemetadata/dats/sync',
      method: 'POST',
      mutationOptions: {
        onSuccess: (result) => {
          const platformsCount = result?.platformsSynced ?? 0;
          const entriesCount = result?.totalEntriesSynced ?? 0;
          setDatImportStatus(
            `Synced online No-Intro, Redump, TOSEC, SmokeMonster SMDB, and NSWDB catalogs across ${platformsCount} platform(s) (${entriesCount.toLocaleString()} entries verified).`
          );
          refetchDats();
        },
        onError: () => {
          setDatImportStatus('Failed to sync online DAT / SMDB catalogs.');
        },
      },
    });

  const { mutate: applyRomPatch, isPending: isApplyingPatch } = useApiMutation<
    RomPatchResult,
    RomPatchRequest
  >({
    path: '/gamemetadata/patches/apply',
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        setPatchStatus(result);
      },
      onError: () => {
        setPatchStatus({
          success: false,
          message: 'Failed to apply ROM patch.',
        });
      },
    },
  });

  const { mutate: importDatFile, isPending: isImportingDat } = useApiMutation<
    DatImportResult,
    DatImportRequest
  >({
    path: '/gamemetadata/dats/import',
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        const count = result?.entryCount ?? 0;
        const name = result?.datFile?.name ?? 'Catalog';
        setDatImportStatus(
          `Imported "${name}" (${count.toLocaleString()} hash entries cached and cross-referenced).`
        );
        setDatServerPath('');
        refetchDats();
      },
      onError: () => {
        setDatImportStatus('Failed to import DAT / SMDB file.');
      },
    },
  });

  useEffect(() => {
    if (backendSettings && !hasPendingChanges) {
      const merged: UnifiedMetadataSettings = {
        generateGamelistXml:
          backendSettings.generateGamelistXml ??
          DEFAULT_SETTINGS.generateGamelistXml,
        scrapers:
          Array.isArray(backendSettings.scrapers) &&
          backendSettings.scrapers.length > 0
            ? backendSettings.scrapers
            : DEFAULT_SCRAPERS,
      };
      setSettings(merged);
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
      } catch {
        // ignore storage errors
      }
    }
  }, [backendSettings, hasPendingChanges]);

  const { mutate: saveBackendSettings } = useApiMutation<
    UnifiedMetadataSettings,
    UnifiedMetadataSettings
  >({
    path: '/gamemetadata/settings',
    method: 'POST',
    mutationOptions: {
      onSuccess: (saved) => {
        if (saved && Array.isArray(saved.scrapers)) {
          setSettings(saved);
          try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(saved));
          } catch {
            // ignore
          }
        }
        setHasPendingChanges(false);
        setSavedNotice(true);
      },
      onError: () => {
        setHasPendingChanges(false);
        setSavedNotice(true);
      },
    },
  });

  const updateScraperField = useCallback(
    <K extends keyof MetadataScraperConfig>(
      scraperId: string,
      field: K,
      value: MetadataScraperConfig[K]
    ) => {
      setSettings((prev) => ({
        ...prev,
        scrapers: prev.scrapers.map((s) =>
          s.id === scraperId ? { ...s, [field]: value } : s
        ),
      }));
      setHasPendingChanges(true);
      setSavedNotice(false);
    },
    []
  );

  const handleGamelistToggle = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setSettings((prev) => ({
        ...prev,
        generateGamelistXml: Boolean(value),
      }));
      setHasPendingChanges(true);
      setSavedNotice(false);
    },
    []
  );

  const handleSavePress = useCallback(() => {
    const ordered: UnifiedMetadataSettings = {
      ...settings,
      scrapers: [...settings.scrapers].sort((a, b) => a.priority - b.priority),
    };
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(ordered));
    saveBackendSettings(ordered);
  }, [settings, saveBackendSettings]);

  const handleDatPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setDatServerPath(value ?? '');
    },
    []
  );

  const handleImportServerPathPress = useCallback(() => {
    if (!datServerPath.trim()) {
      return;
    }

    importDatFile({
      filePath: datServerPath.trim(),
    });
  }, [datServerPath, importDatFile]);

  const handleLocalFileChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const content = typeof reader.result === 'string' ? reader.result : '';
        if (content) {
          importDatFile({
            fileName: file.name,
            content,
          });
        }
      };
      reader.readAsText(file);
      event.target.value = '';
    },
    [importDatFile]
  );

  const handleSyncOnlineDatsPress = useCallback(() => {
    syncOnlineDats({});
  }, [syncOnlineDats]);

  const handlePatchUploadChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const dataUrl = typeof reader.result === 'string' ? reader.result : '';
        if (dataUrl) {
          applyRomPatch({
            patchFileName: file.name,
            patchBase64Content: dataUrl,
            baseRomPath: baseRomServerPath.trim() || undefined,
            outputTitle: patchOutputTitle.trim() || undefined,
          });
        }
      };
      reader.readAsDataURL(file);
      event.target.value = '';
    },
    [applyRomPatch, baseRomServerPath, patchOutputTitle]
  );

  const handleApplyServerPatchPress = useCallback(() => {
    if (!patchServerPath.trim()) {
      return;
    }

    applyRomPatch({
      patchFilePath: patchServerPath.trim(),
      baseRomPath: baseRomServerPath.trim() || undefined,
      outputTitle: patchOutputTitle.trim() || undefined,
    });
  }, [applyRomPatch, patchServerPath, baseRomServerPath, patchOutputTitle]);

  const sortedScrapers = [...settings.scrapers].sort(
    (a, b) => a.priority - b.priority
  );

  return (
    <SettingsPage
      title={translate('MetadataSettings')}
      showSave={true}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('Metadata')}
          />

          {savedNotice ? (
            <Alert kind={kinds.SUCCESS}>
              Metadata scrapers, priorities, rate-limit fail-safes, and asset
              preferences saved.
            </Alert>
          ) : null}

          <Form>
            <FieldSet
              legend="Cross-Referenced ROM & Verification Databases (Always Active)"
              caption="No-Intro, Redump, TOSEC, and SmokeMonster HTGDB/SMDB databases are maintained separately for easy updates and automatically cross-referenced to populate platform libraries without duplicates and verify ROM hashes (CRC32, MD5, SHA-1, SHA-256) on download and manual import."
            >
              {datImportStatus ? (
                <Alert kind={kinds.INFO}>{datImportStatus}</Alert>
              ) : null}

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Active Verification Databases</FormLabel>
                <FormInputHelpText text="Separate databases cross-referenced by cryptographic hash and canonical release key so updates never create duplicates" />
                <div>
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    No-Intro
                  </Label>{' '}
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    Redump
                  </Label>{' '}
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    TOSEC
                  </Label>{' '}
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    SmokeMonster HTGDB / SMDB
                  </Label>
                </div>
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Upload Local DAT or SMDB Update</FormLabel>
                <FormInputHelpText text="Select a .dat, .xml, .txt, or .smdb file to add or update that specific catalog in the cross-referenced database" />
                <input
                  type="file"
                  accept=".dat,.xml,.txt,.smdb"
                  disabled={isImportingDat}
                  onChange={handleLocalFileChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Or Import DAT / SMDB from Server Path</FormLabel>
                <FormInputHelpText text="Full server path to a local No-Intro, Redump, TOSEC (.dat / .xml) or SmokeMonster (.txt / .smdb) file" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="datServerPath"
                  value={datServerPath}
                  placeholder="C:\DATs\Nintendo - Super Nintendo Entertainment System.dat"
                  onChange={handleDatPathChange}
                />
                <div style={{ marginTop: '8px', display: 'flex', gap: '8px' }}>
                  <Button
                    kind={kinds.PRIMARY}
                    size={sizes.SMALL}
                    isDisabled={isImportingDat || !datServerPath.trim()}
                    onPress={handleImportServerPathPress}
                  >
                    {isImportingDat ? 'Importing...' : 'Import Server File'}
                  </Button>
                  <Button
                    kind={kinds.DEFAULT}
                    size={sizes.SMALL}
                    isDisabled={isSyncingOnlineDats}
                    onPress={handleSyncOnlineDatsPress}
                  >
                    {isSyncingOnlineDats
                      ? 'Syncing Online Catalogs...'
                      : 'Sync Online DAT & SMDB Catalogs Now (Daily Scheduled)'}
                  </Button>
                </div>
              </FormRow>

              {importedDats && importedDats.length > 0 ? (
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Cached Verification Databases</FormLabel>
                  <FormInputHelpText
                    text={`${importedDats.length} separate DAT / SMDB database(s) cached and cross-referenced`}
                  />
                  <div>
                    {importedDats.slice(0, 16).map((dat) => (
                      <Label
                        key={dat.id}
                        kind={kinds.DEFAULT}
                        size={sizes.SMALL}
                      >
                        {dat.name}
                        {dat.category ? ` (${dat.category})` : ''}
                      </Label>
                    ))}
                  </div>
                </FormRow>
              ) : null}
            </FieldSet>

            <FieldSet
              legend="Cryptographic Hash-Gated Romhack & Translation Patcher (.ips / .bps / .xdelta)"
              caption="Automatically or manually apply .ips, .bps, and .xdelta / VCDIFF patches onto verified retail base ROMs in your library, outputting the patched copy into the ROM Hacks & Translations folder without modifying your clean 1G1R retail file."
            >
              {patchStatus ? (
                <Alert
                  kind={patchStatus.success ? kinds.SUCCESS : kinds.DANGER}
                >
                  {patchStatus.message}
                  {patchStatus.outputRomPath
                    ? ` → ${patchStatus.outputRomPath}`
                    : ''}
                </Alert>
              ) : null}

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Optional Base Retail ROM Path</FormLabel>
                <FormInputHelpText text="Leave blank to automatically discover the matching retail base ROM in your library by BPS source CRC32 or title, or specify an explicit retail ROM path" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="baseRomServerPath"
                  value={baseRomServerPath}
                  placeholder="(Auto-discover from platform library)"
                  onChange={({ value }: InputChanged<string>) =>
                    setBaseRomServerPath(value ?? '')
                  }
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Optional Output Hack / Translation Title</FormLabel>
                <FormInputHelpText text="Leave blank to use the matched HTGDB / SMDB / DAT canonical name or patch filename" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="patchOutputTitle"
                  value={patchOutputTitle}
                  placeholder="e.g. Mother 3 [T+Eng v1.3] (Hack)"
                  onChange={({ value }: InputChanged<string>) =>
                    setPatchOutputTitle(value ?? '')
                  }
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Upload & Apply Patch File (.ips / .bps / .xdelta / .zip)</FormLabel>
                <FormInputHelpText text="Select a local .ips, .bps, .xdelta, .vcdiff, or .zip patch file to apply onto the verified retail base ROM" />
                <input
                  type="file"
                  accept=".ips,.bps,.xdelta,.vcdiff,.zip"
                  disabled={isApplyingPatch}
                  onChange={handlePatchUploadChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Or Apply Patch from Server Path</FormLabel>
                <FormInputHelpText text="Full server path to a .ips, .bps, .xdelta, .vcdiff, or .zip patch file" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="patchServerPath"
                  value={patchServerPath}
                  placeholder="C:\Patches\Super Mario Bros. 3Mix.bps"
                  onChange={({ value }: InputChanged<string>) =>
                    setPatchServerPath(value ?? '')
                  }
                />
                <div style={{ marginTop: '8px' }}>
                  <Button
                    kind={kinds.PRIMARY}
                    size={sizes.SMALL}
                    isDisabled={isApplyingPatch || !patchServerPath.trim()}
                    onPress={handleApplyServerPatchPress}
                  >
                    {isApplyingPatch
                      ? 'Applying Patch...'
                      : 'Apply Patch to Retail Base ROM'}
                  </Button>
                </div>
              </FormRow>
            </FieldSet>

            {sortedScrapers.map((scraper) => (
              <FieldSet
                key={scraper.id}
                legend={`${scraper.name} (Priority ${scraper.priority})`}
                caption={`${scraper.description}. Controllarr cascades through enabled scrapers in priority order to complete missing assets without overwriting existing files.`}
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Enable {scraper.name}</FormLabel>
                  <FormInputHelpText text="Gather basic metadata on library creation and selected artwork/media assets upon ROM import" />
                  <CheckInput
                    name={`${scraper.id}_enabled`}
                    value={scraper.enabled}
                    onChange={({ value }) =>
                      updateScraperField(scraper.id, 'enabled', Boolean(value))
                    }
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Scraper Priority (1 = Highest)</FormLabel>
                  <FormInputHelpText text="Lower numbers run first; if an asset is missing or rate limits are reached, Controllarr moves to the next priority source to complete the data" />
                  <FormInput
                    type={inputTypes.NUMBER}
                    name={`${scraper.id}_priority`}
                    value={scraper.priority}
                    min={1}
                    max={10}
                    onChange={({ value }) =>
                      updateScraperField(
                        scraper.id,
                        'priority',
                        Math.max(1, Number(value) || 1)
                      )
                    }
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>
                    API Rate-Limit Fail-Safe (Max Requests / Minute)
                  </FormLabel>
                  <FormInputHelpText text="Protects external APIs from bombardment and automatically cools down and cascades to the next scraper on HTTP 429/503" />
                  <FormInput
                    type={inputTypes.NUMBER}
                    name={`${scraper.id}_maxRequestsPerMinute`}
                    value={scraper.maxRequestsPerMinute}
                    min={5}
                    max={600}
                    onChange={({ value }) =>
                      updateScraperField(
                        scraper.id,
                        'maxRequestsPerMinute',
                        Math.max(5, Number(value) || 30)
                      )
                    }
                  />
                </FormRow>

                {scraper.enabled && scraper.id === 'screenscraper' ? (
                  <>
                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>ScreenScraper Username</FormLabel>
                      <FormInputHelpText text="Your registered ScreenScraper.fr member username" />
                      <FormInput
                        type={inputTypes.TEXT}
                        name={`${scraper.id}_username`}
                        value={scraper.username}
                        placeholder="ScreenScraper username"
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'username',
                            String(value ?? '')
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>ScreenScraper Password</FormLabel>
                      <FormInputHelpText text="Your ScreenScraper.fr account password" />
                      <FormInput
                        type={inputTypes.PASSWORD}
                        name={`${scraper.id}_password`}
                        value={scraper.password}
                        placeholder="ScreenScraper password"
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'password',
                            String(value ?? '')
                          )
                        }
                      />
                    </FormRow>
                  </>
                ) : null}

                {scraper.enabled && scraper.id === 'igdb' ? (
                  <>
                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Twitch / IGDB Client ID</FormLabel>
                      <FormInputHelpText text="Client ID from the Twitch Developer Console (dev.twitch.tv)" />
                      <FormInput
                        type={inputTypes.TEXT}
                        name={`${scraper.id}_clientId`}
                        value={scraper.clientId}
                        placeholder="Twitch Client ID"
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'clientId',
                            String(value ?? '')
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Twitch / IGDB Client Secret</FormLabel>
                      <FormInputHelpText text="Client Secret from the Twitch Developer Console" />
                      <FormInput
                        type={inputTypes.PASSWORD}
                        name={`${scraper.id}_clientSecret`}
                        value={scraper.clientSecret}
                        placeholder="Twitch Client Secret"
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'clientSecret',
                            String(value ?? '')
                          )
                        }
                      />
                    </FormRow>
                  </>
                ) : null}

                {scraper.enabled && scraper.id === 'thegamesdb' ? (
                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>TheGamesDB API Key</FormLabel>
                    <FormInputHelpText text="Public or private API key from thegamesdb.net" />
                    <FormInput
                      type={inputTypes.PASSWORD}
                      name={`${scraper.id}_apiKey`}
                      value={scraper.apiKey}
                      placeholder="TheGamesDB API Key"
                      onChange={({ value }) =>
                        updateScraperField(
                          scraper.id,
                          'apiKey',
                          String(value ?? '')
                        )
                      }
                    />
                  </FormRow>
                ) : null}

                {scraper.enabled ? (
                  <>
                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Publishing &amp; Release Metadata</FormLabel>
                      <FormInputHelpText text="Gather synopsis, release date, developer, publisher, and genre metadata from this source" />
                      <CheckInput
                        name={`${scraper.id}_publishingMetadata`}
                        value={scraper.publishingMetadata}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'publishingMetadata',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>2D Box Art (media/box2d)</FormLabel>
                      <FormInputHelpText text="Download regional 2D front box art from this source if not already present" />
                      <CheckInput
                        name={`${scraper.id}_boxArt2d`}
                        value={scraper.boxArt2d}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'boxArt2d',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>3D Box Art Renders (media/box3d)</FormLabel>
                      <FormInputHelpText text="Download angled 3D box art renders from this source if not already present" />
                      <CheckInput
                        name={`${scraper.id}_boxArt3d`}
                        value={scraper.boxArt3d}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'boxArt3d',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Gameplay Screenshots (media/screenshots)</FormLabel>
                      <FormInputHelpText text="Download in-game screenshots from this source if not already present" />
                      <CheckInput
                        name={`${scraper.id}_screenshots`}
                        value={scraper.screenshots}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'screenshots',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Title Screens (media/titlescreens)</FormLabel>
                      <FormInputHelpText text="Download title screen captures from this source if not already present" />
                      <CheckInput
                        name={`${scraper.id}_titleScreens`}
                        value={scraper.titleScreens}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'titleScreens',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Clear Logos / Wheel Art (media/marquee)</FormLabel>
                      <FormInputHelpText text="Download transparent clear logos / marquee art from this source if not already present" />
                      <CheckInput
                        name={`${scraper.id}_clearLogos`}
                        value={scraper.clearLogos}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'clearLogos',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>
                        Gameplay Video Previews (media/videos/*.mp4)
                      </FormLabel>
                      <FormInputHelpText text="Download short gameplay video snaps (.mp4) on ROM import if not already present" />
                      <CheckInput
                        name={`${scraper.id}_videoPreviews`}
                        value={scraper.videoPreviews}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'videoPreviews',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>
                        Instruction Manuals (media/manuals/*.pdf)
                      </FormLabel>
                      <FormInputHelpText text="Download scanned instruction manuals (.pdf) on ROM import if not already present" />
                      <CheckInput
                        name={`${scraper.id}_manualsPdf`}
                        value={scraper.manualsPdf}
                        onChange={({ value }) =>
                          updateScraperField(
                            scraper.id,
                            'manualsPdf',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>
                  </>
                ) : null}
              </FieldSet>
            ))}

            <FieldSet
              legend="Emulation Frontend Manifest & Export Integration"
              caption="Save downloaded artwork and gamelist.xml inside each platform folder (<Platform>/media/<type>/ and <Platform>/gamelist.xml) for seamless SD card and flash-cart export"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Generate EmulationStation gamelist.xml</FormLabel>
                <FormInputHelpText text="Write and update gamelist.xml inside each platform folder referencing ./media/box2d, ./media/marquee, ./media/videos, and ./media/manuals for ES-DE, Batocera, Knulli, OnionOS, and RetroBat" />
                <CheckInput
                  name="generateGamelistXml"
                  value={settings.generateGamelistXml}
                  onChange={handleGamelistToggle}
                />
              </FormRow>
            </FieldSet>
          </Form>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default MetadataSettings;
