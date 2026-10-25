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
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SettingsCard from 'Components/SettingsCard/SettingsCard';
import settingsCardStyles from 'Components/SettingsCard/SettingsCard.module.css';
import SettingsCardStatus from 'Components/SettingsCard/SettingsCardStatus';
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
    videoPreviews: false,
    manualsPdf: false,
  },
  {
    id: 'igdb',
    name: 'IGDB (Twitch Developer API)',
    description:
      'Rich game synopses, developer/publisher history, release dates, genres, cover art, and screenshots',
    requiresCredentials: true,
    enabled: false,
    priority: 2,
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
    titleScreens: false,
    clearLogos: true,
    videoPreviews: false,
    manualsPdf: false,
  },
  {
    id: 'thegamesdb',
    name: 'TheGamesDB (TGDB)',
    description:
      'Community console database with regional box art, fanart, clear logos, and release metadata',
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
      'Keyless public CDN for Named_Boxarts, Named_Snaps, Named_Titles, and LaunchBox community metadata',
    requiresCredentials: false,
    enabled: true,
    priority: 4,
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
    clearLogos: true,
    videoPreviews: false,
    manualsPdf: false,
  },
];

const DEFAULT_SETTINGS: UnifiedMetadataSettings = {
  generateGamelistXml: true,
  scrapers: DEFAULT_SCRAPERS,
};

function mergeWithDefaults(
  raw?: Partial<UnifiedMetadataSettings> | null
): UnifiedMetadataSettings {
  if (!raw) {
    return DEFAULT_SETTINGS;
  }

  const mergedScrapers = DEFAULT_SCRAPERS.map((def) => {
    const found = Array.isArray(raw.scrapers)
      ? raw.scrapers.find((s) => s.id === def.id)
      : undefined;
    return found ? { ...def, ...found } : def;
  });

  return {
    generateGamelistXml:
      raw.generateGamelistXml ?? DEFAULT_SETTINGS.generateGamelistXml,
    scrapers: mergedScrapers,
  };
}

function loadLocalMetadataSettings(): UnifiedMetadataSettings {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return DEFAULT_SETTINGS;
    }

    return mergeWithDefaults(JSON.parse(raw) as Partial<UnifiedMetadataSettings>);
  } catch {
    return DEFAULT_SETTINGS;
  }
}

function getEnabledAssetLabels(scraper: MetadataScraperConfig): string[] {
  const labels: string[] = [];
  if (scraper.publishingMetadata) {
    labels.push('Metadata');
  }
  if (scraper.boxArt2d) {
    labels.push('2D Box');
  }
  if (scraper.boxArt3d) {
    labels.push('3D Box');
  }
  if (scraper.screenshots) {
    labels.push('Screenshots');
  }
  if (scraper.titleScreens) {
    labels.push('Title Screens');
  }
  if (scraper.clearLogos) {
    labels.push('Clear Logos');
  }
  if (scraper.videoPreviews) {
    labels.push('Videos');
  }
  if (scraper.manualsPdf) {
    labels.push('Manuals');
  }
  return labels;
}

function MetadataSettings() {
  const [settings, setSettings] = useState<UnifiedMetadataSettings>(
    loadLocalMetadataSettings
  );
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);
  const [editingScraperId, setEditingScraperId] = useState<string | null>(null);
  const [datServerPath, setDatServerPath] = useState('');
  const [datImportStatus, setDatImportStatus] = useState<string | null>(null);
  const [patchServerPath, setPatchServerPath] = useState('');
  const [baseRomServerPath, setBaseRomServerPath] = useState('');
  const [patchOutputTitle, setPatchOutputTitle] = useState('');
  const [patchStatus, setPatchStatus] = useState<RomPatchResult | null>(null);

  const { data: serverSettings } = useApiQuery<UnifiedMetadataSettings>({
    path: '/gamemetadata/settings',
  });

  const { data: importedDats, refetch: refetchDats } = useApiQuery<
    DatFileRecord[]
  >({
    path: '/gamemetadata/dats',
  });

  useEffect(() => {
    if (serverSettings) {
      const merged = mergeWithDefaults(serverSettings);
      setSettings(merged);
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
      } catch {
        // Ignore storage quota errors
      }
    }
  }, [serverSettings]);

  const { mutate: saveToServer } = useApiMutation<
    UnifiedMetadataSettings,
    UnifiedMetadataSettings
  >({
    path: '/gamemetadata/settings',
    method: 'POST',
    mutationOptions: {
      onSuccess: (saved) => {
        const merged = mergeWithDefaults(saved);
        setSettings(merged);
        try {
          window.localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
        } catch {
          // Ignore storage quota errors
        }
        setHasPendingChanges(false);
        setSavedNotice(true);
      },
    },
  });

  const { mutate: importDatCatalog, isPending: isImportingDat } =
    useApiMutation<DatImportResult, DatImportRequest>({
      path: '/gamemetadata/dats/import',
      method: 'POST',
      mutationOptions: {
        onSuccess: (result) => {
          setDatImportStatus(
            `Imported ${result.datFile?.name ?? 'catalog'} (${result.entryCount} verified ROM entries cached and cross-referenced).`
          );
          setDatServerPath('');
          refetchDats();
        },
        onError: () => {
          setDatImportStatus(
            'Unable to parse or import the specified DAT/SMDB file. Verify the path and format (.dat XML/ClrMamePro or .txt/.smdb).'
          );
        },
      },
    });

  const { mutate: syncOnlineDatCatalogs, isPending: isSyncingOnlineDats } =
    useApiMutation<DatCatalogSyncResult, Record<string, never>>({
      path: '/gamemetadata/dats/sync',
      method: 'POST',
      mutationOptions: {
        onSuccess: (result) => {
          setDatImportStatus(
            `Synced online No-Intro / Redump / TOSEC / SmokeMonster SMDB catalogs across ${result.platformsSynced} library platform(s) (${result.totalEntriesSynced} verified ROM entries refreshed).`
          );
          refetchDats();
        },
        onError: () => {
          setDatImportStatus(
            'Online DAT catalog sync encountered an error. Check network connectivity.'
          );
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
        setPatchServerPath('');
      },
      onError: () => {
        setPatchStatus({
          success: false,
          message:
            'Failed to apply patch. Ensure the patch file (.ips, .bps, .xdelta) and matching retail base ROM are valid.',
        });
      },
    },
  });

  const persistSettings = useCallback(
    (nextSettings: UnifiedMetadataSettings) => {
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(nextSettings));
      } catch {
        // Ignore storage quota errors
      }
      saveToServer(nextSettings);
    },
    [saveToServer]
  );

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
    persistSettings(settings);
  }, [persistSettings, settings]);

  const handleModalSavePress = useCallback(() => {
    persistSettings(settings);
    setEditingScraperId(null);
  }, [persistSettings, settings]);

  const handleDatPathChange = useCallback(({ value }: InputChanged<string>) => {
    setDatServerPath(value ?? '');
  }, []);

  const handleImportServerPathPress = useCallback(() => {
    if (!datServerPath.trim()) {
      return;
    }

    importDatCatalog({
      filePath: datServerPath.trim(),
    });
  }, [datServerPath, importDatCatalog]);

  const handleLocalFileChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const text = typeof reader.result === 'string' ? reader.result : '';
        if (text) {
          importDatCatalog({
            fileName: file.name,
            content: text,
          });
        }
      };
      reader.readAsText(file);
      event.target.value = '';
    },
    [importDatCatalog]
  );

  const handleSyncOnlineDatsPress = useCallback(() => {
    syncOnlineDatCatalogs({});
  }, [syncOnlineDatCatalogs]);

  const handlePatchUploadChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const dataUrl = typeof reader.result === 'string' ? reader.result : '';
        const commaIdx = dataUrl.indexOf(',');
        const base64 = commaIdx >= 0 ? dataUrl.slice(commaIdx + 1) : dataUrl;
        if (base64) {
          applyRomPatch({
            patchFileName: file.name,
            patchBase64Content: base64,
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

  const editingScraper =
    settings.scrapers.find((s) => s.id === editingScraperId) ?? null;

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

          <FieldSet
            legend="Metadata & Artwork Scrapers"
            caption="Click any scraper card below to configure credentials, priority order (1 = highest), API rate-limit fail-safes, and artwork types. Controllarr cascades through enabled scrapers in priority order without overwriting existing files."
          >
            <div className={settingsCardStyles.grid}>
              {sortedScrapers.map((scraper) => {
                const assetLabels = getEnabledAssetLabels(scraper);
                return (
                  <SettingsCard
                    key={scraper.id}
                    name={scraper.name}
                    aria-label={`Edit ${scraper.name}`}
                    onPress={() => setEditingScraperId(scraper.id)}
                  >
                    <SettingsCardStatus
                      dot={scraper.enabled ? 'active' : 'muted'}
                      segments={[
                        translate(scraper.enabled ? 'Enabled' : 'Disabled'),
                        `Priority: ${scraper.priority}`,
                        `${scraper.maxRequestsPerMinute} req/min`,
                      ]}
                    />

                    <div className={settingsCardStyles.labels}>
                      {assetLabels.map((lbl) => (
                        <Label
                          key={lbl}
                          kind={
                            scraper.enabled ? kinds.PRIMARY : kinds.DEFAULT
                          }
                          size={sizes.SMALL}
                        >
                          {lbl}
                        </Label>
                      ))}
                    </div>
                  </SettingsCard>
                );
              })}
            </div>
          </FieldSet>

          <Form>
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
                  placeholder="/path/to/Nintendo - Super Nintendo Entertainment System.dat"
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
                <FormLabel>
                  Upload &amp; Apply Patch File (.ips / .bps / .xdelta / .zip)
                </FormLabel>
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
                  placeholder="/path/to/Super Mario Bros. 3Mix.bps"
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
          </Form>

          <Modal
            isOpen={Boolean(editingScraper)}
            size={sizes.MEDIUM}
            onModalClose={() => setEditingScraperId(null)}
          >
            {editingScraper ? (
              <ModalContent onModalClose={() => setEditingScraperId(null)}>
                <ModalHeader>
                  Edit Metadata Scraper - {editingScraper.name}
                </ModalHeader>

                <ModalBody>
                  <Form>
                    <Alert kind={kinds.INFO}>
                      {editingScraper.description}
                    </Alert>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Enable</FormLabel>
                      <FormInputHelpText text="Enable this metadata and artwork scraper" />
                      <CheckInput
                        name={`${editingScraper.id}_enabled`}
                        value={editingScraper.enabled}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'enabled',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Priority (1 = Highest)</FormLabel>
                      <FormInputHelpText text="Lower numbers run first; Controllarr cascades to the next scraper for any missing assets" />
                      <FormInput
                        type={inputTypes.NUMBER}
                        name={`${editingScraper.id}_priority`}
                        value={editingScraper.priority}
                        min={1}
                        max={10}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'priority',
                            Math.max(1, Number(value) || 1)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Max Requests / Minute</FormLabel>
                      <FormInputHelpText text="API rate-limit fail-safe; automatically cools down and cascades on HTTP 429/503" />
                      <FormInput
                        type={inputTypes.NUMBER}
                        name={`${editingScraper.id}_maxRequestsPerMinute`}
                        value={editingScraper.maxRequestsPerMinute}
                        min={5}
                        max={600}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'maxRequestsPerMinute',
                            Math.max(5, Number(value) || 30)
                          )
                        }
                      />
                    </FormRow>

                    {editingScraper.id === 'screenscraper' ? (
                      <>
                        <FormRow size={sizes.MEDIUM}>
                          <FormLabel>ScreenScraper Username</FormLabel>
                          <FormInputHelpText text="Your registered ScreenScraper.fr member username" />
                          <FormInput
                            type={inputTypes.TEXT}
                            name={`${editingScraper.id}_username`}
                            value={editingScraper.username}
                            placeholder="ScreenScraper username"
                            onChange={({ value }) =>
                              updateScraperField(
                                editingScraper.id,
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
                            name={`${editingScraper.id}_password`}
                            value={editingScraper.password}
                            placeholder="ScreenScraper password"
                            onChange={({ value }) =>
                              updateScraperField(
                                editingScraper.id,
                                'password',
                                String(value ?? '')
                              )
                            }
                          />
                        </FormRow>
                      </>
                    ) : null}

                    {editingScraper.id === 'igdb' ? (
                      <>
                        <FormRow size={sizes.MEDIUM}>
                          <FormLabel>Twitch / IGDB Client ID</FormLabel>
                          <FormInputHelpText text="Client ID from the Twitch Developer Console (dev.twitch.tv)" />
                          <FormInput
                            type={inputTypes.TEXT}
                            name={`${editingScraper.id}_clientId`}
                            value={editingScraper.clientId}
                            placeholder="Twitch Client ID"
                            onChange={({ value }) =>
                              updateScraperField(
                                editingScraper.id,
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
                            name={`${editingScraper.id}_clientSecret`}
                            value={editingScraper.clientSecret}
                            placeholder="Twitch Client Secret"
                            onChange={({ value }) =>
                              updateScraperField(
                                editingScraper.id,
                                'clientSecret',
                                String(value ?? '')
                              )
                            }
                          />
                        </FormRow>
                      </>
                    ) : null}

                    {editingScraper.id === 'thegamesdb' ? (
                      <FormRow size={sizes.MEDIUM}>
                        <FormLabel>TheGamesDB API Key</FormLabel>
                        <FormInputHelpText text="Public or private API key from thegamesdb.net" />
                        <FormInput
                          type={inputTypes.PASSWORD}
                          name={`${editingScraper.id}_apiKey`}
                          value={editingScraper.apiKey}
                          placeholder="TheGamesDB API Key"
                          onChange={({ value }) =>
                            updateScraperField(
                              editingScraper.id,
                              'apiKey',
                              String(value ?? '')
                            )
                          }
                        />
                      </FormRow>
                    ) : null}

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Publishing &amp; Release Metadata</FormLabel>
                      <FormInputHelpText text="Synopsis, release date, developer, publisher, and genres" />
                      <CheckInput
                        name={`${editingScraper.id}_publishingMetadata`}
                        value={editingScraper.publishingMetadata}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'publishingMetadata',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>2D Box Art (media/box2d)</FormLabel>
                      <FormInputHelpText text="Regional 2D front box art" />
                      <CheckInput
                        name={`${editingScraper.id}_boxArt2d`}
                        value={editingScraper.boxArt2d}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'boxArt2d',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>3D Box Art Renders (media/box3d)</FormLabel>
                      <FormInputHelpText text="Angled 3D box art renders" />
                      <CheckInput
                        name={`${editingScraper.id}_boxArt3d`}
                        value={editingScraper.boxArt3d}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'boxArt3d',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>
                        Gameplay Screenshots (media/screenshots)
                      </FormLabel>
                      <FormInputHelpText text="In-game gameplay screenshots" />
                      <CheckInput
                        name={`${editingScraper.id}_screenshots`}
                        value={editingScraper.screenshots}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'screenshots',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Title Screens (media/titlescreens)</FormLabel>
                      <FormInputHelpText text="Title screen captures" />
                      <CheckInput
                        name={`${editingScraper.id}_titleScreens`}
                        value={editingScraper.titleScreens}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'titleScreens',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>
                        Clear Logos / Wheel Art (media/marquee)
                      </FormLabel>
                      <FormInputHelpText text="Transparent clear logos / marquee art" />
                      <CheckInput
                        name={`${editingScraper.id}_clearLogos`}
                        value={editingScraper.clearLogos}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
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
                      <FormInputHelpText text="Short gameplay video snaps (.mp4)" />
                      <CheckInput
                        name={`${editingScraper.id}_videoPreviews`}
                        value={editingScraper.videoPreviews}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
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
                      <FormInputHelpText text="Scanned instruction manuals (.pdf)" />
                      <CheckInput
                        name={`${editingScraper.id}_manualsPdf`}
                        value={editingScraper.manualsPdf}
                        onChange={({ value }) =>
                          updateScraperField(
                            editingScraper.id,
                            'manualsPdf',
                            Boolean(value)
                          )
                        }
                      />
                    </FormRow>
                  </Form>
                </ModalBody>

                <ModalFooter>
                  <Button onPress={() => setEditingScraperId(null)}>
                    {translate('Cancel')}
                  </Button>
                  <Button kind={kinds.PRIMARY} onPress={handleModalSavePress}>
                    {translate('Save')}
                  </Button>
                </ModalFooter>
              </ModalContent>
            ) : null}
          </Modal>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default MetadataSettings;
