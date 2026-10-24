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

interface MetadataSourceConfig {
  enableNoIntroLibretro: boolean;
  enableHtgdbSmdb: boolean;
  enableIgdb: boolean;
  igdbClientId: string;
  igdbClientSecret: string;
  enableScreenScraper: boolean;
  screenScraperUsername: string;
  screenScraperPassword: string;
  enableTheGamesDb: boolean;
  theGamesDbApiKey: string;
  enableLaunchBox: boolean;
}

const STORAGE_KEY = 'controllarr_metadata_sources_v1';

const DEFAULT_CONFIG: MetadataSourceConfig = {
  enableNoIntroLibretro: true,
  enableHtgdbSmdb: true,
  enableIgdb: false,
  igdbClientId: '',
  igdbClientSecret: '',
  enableScreenScraper: false,
  screenScraperUsername: '',
  screenScraperPassword: '',
  enableTheGamesDb: false,
  theGamesDbApiKey: '',
  enableLaunchBox: true,
};

function loadInitialConfig(): MetadataSourceConfig {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);

    if (raw) {
      return { ...DEFAULT_CONFIG, ...JSON.parse(raw) };
    }
  } catch {
    // ignore storage errors
  }

  return DEFAULT_CONFIG;
}

function MetadataSourceSettings() {
  const [config, setConfig] = useState<MetadataSourceConfig>(loadInitialConfig);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);
  const [datServerPath, setDatServerPath] = useState('');
  const [datImportStatus, setDatImportStatus] = useState<string | null>(null);

  const { data: backendConfig } = useApiQuery<MetadataSourceConfig>({
    path: '/gamemetadata/sources',
  });

  const { data: importedDats, refetch: refetchDats } = useApiQuery<
    DatFileRecord[]
  >({
    path: '/gamemetadata/dats',
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
          `Imported "${name}" (${count.toLocaleString()} hash entries cached).`
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
    if (backendConfig && !hasPendingChanges) {
      const merged = { ...DEFAULT_CONFIG, ...backendConfig };
      setConfig(merged);
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
      } catch {
        // ignore storage errors
      }
    }
  }, [backendConfig, hasPendingChanges]);

  const { mutate: saveBackendSources } = useApiMutation<
    MetadataSourceConfig,
    MetadataSourceConfig
  >({
    path: '/gamemetadata/sources',
    method: 'POST',
    mutationOptions: {
      onSuccess: (saved) => {
        if (saved) {
          const merged = { ...DEFAULT_CONFIG, ...saved };
          setConfig(merged);
          try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
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

  const handleCheckChange = useCallback(
    ({ name, value }: InputChanged<boolean>) => {
      setConfig((prev) => ({
        ...prev,
        [name]: Boolean(value),
      }));
      setHasPendingChanges(true);
      setSavedNotice(false);
    },
    []
  );

  const handleTextChange = useCallback(
    ({ name, value }: InputChanged<string>) => {
      setConfig((prev) => ({
        ...prev,
        [name]: value ?? '',
      }));
      setHasPendingChanges(true);
      setSavedNotice(false);
    },
    []
  );

  const handleSavePress = useCallback(() => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
    saveBackendSources(config);
  }, [config, saveBackendSources]);

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

  return (
    <SettingsPage
      title={translate('MetadataSourceSettings')}
      showSave={true}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('MetadataSource')}
          />

          {savedNotice ? (
            <Alert kind={kinds.SUCCESS}>
              Metadata sources and provider credentials saved.
            </Alert>
          ) : null}

          <Form>
            <FieldSet
              legend="Built-In ROM & Catalog Databases (No Credentials Required)"
              caption="Authoritative DAT and hardware target databases used for catalog pre-population, regional console aliases, and cryptographic hash verification"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  No-Intro / Redump / Libretro DATs &amp; Thumbnails{' '}
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    Primary
                  </Label>
                </FormLabel>
                <FormInputHelpText text="Official No-Intro and Redump game catalogs, regional revisions, and Libretro box art repository" />
                <CheckInput
                  name="enableNoIntroLibretro"
                  value={config.enableNoIntroLibretro}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  SmokeMonster HTGDB / SMDB (Hardware Target Game Database)
                </FormLabel>
                <FormInputHelpText text="Curated hardware target databases, regional aliases (Famicom / Mega Drive / PC Engine), and hash-gated homebrew/ROM hack sets" />
                <CheckInput
                  name="enableHtgdbSmdb"
                  value={config.enableHtgdbSmdb}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>LaunchBox Community Games Database</FormLabel>
                <FormInputHelpText text="Supplemental release dates, publisher metadata, and regional box art" />
                <CheckInput
                  name="enableLaunchBox"
                  value={config.enableLaunchBox}
                  onChange={handleCheckChange}
                />
              </FormRow>
            </FieldSet>

            <FieldSet
              legend="Local DAT / SMDB Hash Catalog Import"
              caption="Import local No-Intro / Redump / TOSEC XML or ClrMamePro (.dat / .xml) and SmokeMonster HTGDB (.txt / .smdb) files into the local cryptographic hash cache"
            >
              {datImportStatus ? (
                <Alert kind={kinds.INFO}>{datImportStatus}</Alert>
              ) : null}

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Upload Local DAT or SMDB File</FormLabel>
                <FormInputHelpText text="Select a .dat, .xml, .txt, or .smdb file from your computer to parse and cache into Controllarr" />
                <input
                  type="file"
                  accept=".dat,.xml,.txt,.smdb"
                  disabled={isImportingDat}
                  onChange={handleLocalFileChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Or Import from Server Path</FormLabel>
                <FormInputHelpText text="Full server path to a local .dat or SMDB .txt file" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="datServerPath"
                  value={datServerPath}
                  placeholder="C:\DATs\Nintendo - Super Nintendo Entertainment System.dat"
                  onChange={handleDatPathChange}
                />
                <div style={{ marginTop: '8px' }}>
                  <Button
                    kind={kinds.PRIMARY}
                    size={sizes.SMALL}
                    isDisabled={isImportingDat || !datServerPath.trim()}
                    onPress={handleImportServerPathPress}
                  >
                    {isImportingDat ? 'Importing...' : 'Import Server File'}
                  </Button>
                </div>
              </FormRow>

              {importedDats && importedDats.length > 0 ? (
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Cached Local Catalogs</FormLabel>
                  <FormInputHelpText
                    text={`${importedDats.length} local DAT / SMDB catalog(s) cached in database`}
                  />
                  <div>
                    {importedDats.slice(0, 12).map((dat) => (
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
              legend="ScreenScraper.fr (Box Art, 3D Renders, Video Snaps & Manuals)"
              caption="Connect your ScreenScraper account to download 3D box art, gameplay video previews (.mp4), manuals (.pdf), and clear logos"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Enable ScreenScraper</FormLabel>
                <FormInputHelpText text="Use ScreenScraper.fr for rich regional artwork, video snaps, and manuals" />
                <CheckInput
                  name="enableScreenScraper"
                  value={config.enableScreenScraper}
                  onChange={handleCheckChange}
                />
              </FormRow>

              {config.enableScreenScraper ? (
                <>
                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>ScreenScraper Username</FormLabel>
                    <FormInputHelpText text="Your registered ScreenScraper.fr member username" />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="screenScraperUsername"
                      value={config.screenScraperUsername}
                      placeholder="ScreenScraper username"
                      onChange={handleTextChange}
                    />
                  </FormRow>

                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>ScreenScraper Password</FormLabel>
                    <FormInputHelpText text="Your ScreenScraper.fr account password" />
                    <FormInput
                      type={inputTypes.PASSWORD}
                      name="screenScraperPassword"
                      value={config.screenScraperPassword}
                      placeholder="ScreenScraper password"
                      onChange={handleTextChange}
                    />
                  </FormRow>
                </>
              ) : null}
            </FieldSet>

            <FieldSet
              legend="IGDB (Internet Game Database via Twitch Developer API)"
              caption="Optional rich game synopses, franchise links, and high-resolution cover art from IGDB"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Enable IGDB</FormLabel>
                <FormInputHelpText text="Query IGDB for game summaries, ratings, and cover artwork" />
                <CheckInput
                  name="enableIgdb"
                  value={config.enableIgdb}
                  onChange={handleCheckChange}
                />
              </FormRow>

              {config.enableIgdb ? (
                <>
                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>Twitch / IGDB Client ID</FormLabel>
                    <FormInputHelpText text="Client ID from the Twitch Developer Console (dev.twitch.tv)" />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="igdbClientId"
                      value={config.igdbClientId}
                      placeholder="Twitch Client ID"
                      onChange={handleTextChange}
                    />
                  </FormRow>

                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>Twitch / IGDB Client Secret</FormLabel>
                    <FormInputHelpText text="Client Secret from the Twitch Developer Console" />
                    <FormInput
                      type={inputTypes.PASSWORD}
                      name="igdbClientSecret"
                      value={config.igdbClientSecret}
                      placeholder="Twitch Client Secret"
                      onChange={handleTextChange}
                    />
                  </FormRow>
                </>
              ) : null}
            </FieldSet>

            <FieldSet
              legend="TheGamesDB (TGDB)"
              caption="Optional community database for retro console box art and platform metadata"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Enable TheGamesDB</FormLabel>
                <FormInputHelpText text="Query TheGamesDB API for box art and release info" />
                <CheckInput
                  name="enableTheGamesDb"
                  value={config.enableTheGamesDb}
                  onChange={handleCheckChange}
                />
              </FormRow>

              {config.enableTheGamesDb ? (
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>TheGamesDB API Key</FormLabel>
                  <FormInputHelpText text="Public or private API key from thegamesdb.net" />
                  <FormInput
                    type={inputTypes.PASSWORD}
                    name="theGamesDbApiKey"
                    value={config.theGamesDbApiKey}
                    placeholder="TheGamesDB API Key"
                    onChange={handleTextChange}
                  />
                </FormRow>
              ) : null}
            </FieldSet>
          </Form>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default MetadataSourceSettings;
