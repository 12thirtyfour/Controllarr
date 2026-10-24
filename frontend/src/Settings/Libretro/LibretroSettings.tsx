import React, { useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import Label from 'Components/Label';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './LibretroSettings.module.css';

export interface LibretroLauncherConfig {
  enableWebPlayer: boolean;
  preferredLaunchMode: string;
  retroArchExecutablePath: string;
  libretroCoresDirectory: string;
  retroArchConfigPath: string;
  emulatorJsDataPath: string;
  defaultShader: string;
  autoInjectBios: boolean;
  persistBatterySaves: boolean;
  enableRetroAchievementsInPlayer: boolean;
  fullScreenOnLaunch: boolean;
  coreOverrides: Record<string, string>;
}

export interface LibretroPlatformCoreOption {
  platformKey: string;
  platformName: string;
  defaultCore: string;
  selectedCore: string;
  availableCores: string[];
  supportsWebAssembly: boolean;
  emulatorJsCoreName: string;
}

export const LIBRETRO_STORAGE_KEY = 'controllarr_libretro_config_v1';

const DEFAULT_CONFIG: LibretroLauncherConfig = {
  enableWebPlayer: true,
  preferredLaunchMode: 'web',
  retroArchExecutablePath: '',
  libretroCoresDirectory: '',
  retroArchConfigPath: '',
  emulatorJsDataPath: 'https://cdn.emulatorjs.org/stable/data/',
  defaultShader: 'none',
  autoInjectBios: true,
  persistBatterySaves: true,
  enableRetroAchievementsInPlayer: true,
  fullScreenOnLaunch: false,
  coreOverrides: {},
};

const FALLBACK_PLATFORMS: LibretroPlatformCoreOption[] = [
  {
    platformKey: 'NES',
    platformName: 'Nintendo Entertainment System / Famicom',
    defaultCore: 'nestopia',
    selectedCore: 'nestopia',
    availableCores: ['nestopia', 'fceumm', 'mesen', 'quicknes'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'nes',
  },
  {
    platformKey: 'SNES',
    platformName: 'Super Nintendo / Super Famicom',
    defaultCore: 'snes9x',
    selectedCore: 'snes9x',
    availableCores: ['snes9x', 'snes9x2010', 'bsnes', 'mesen-s'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'snes',
  },
  {
    platformKey: 'Game Boy',
    platformName: 'Nintendo Game Boy',
    defaultCore: 'sameboy',
    selectedCore: 'sameboy',
    availableCores: ['sameboy', 'gambatte', 'mgba', 'gearboy'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'gb',
  },
  {
    platformKey: 'Game Boy Color',
    platformName: 'Nintendo Game Boy Color',
    defaultCore: 'sameboy',
    selectedCore: 'sameboy',
    availableCores: ['sameboy', 'gambatte', 'mgba', 'gearboy'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'gbc',
  },
  {
    platformKey: 'Game Boy Advance',
    platformName: 'Nintendo Game Boy Advance',
    defaultCore: 'mgba',
    selectedCore: 'mgba',
    availableCores: ['mgba', 'vba_next', 'gpsp', 'beetle_gba'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'gba',
  },
  {
    platformKey: 'Nintendo 64',
    platformName: 'Nintendo 64',
    defaultCore: 'mupen64plus_next',
    selectedCore: 'mupen64plus_next',
    availableCores: ['mupen64plus_next', 'parallel_n64'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'n64',
  },
  {
    platformKey: 'Nintendo DS',
    platformName: 'Nintendo DS',
    defaultCore: 'melonds',
    selectedCore: 'melonds',
    availableCores: ['melonds', 'desmume', 'desmume2015'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'nds',
  },
  {
    platformKey: 'Genesis',
    platformName: 'Sega Genesis / Mega Drive',
    defaultCore: 'genesis_plus_gx',
    selectedCore: 'genesis_plus_gx',
    availableCores: ['genesis_plus_gx', 'picodrive', 'blastem'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'segaMD',
  },
  {
    platformKey: 'Master System',
    platformName: 'Sega Master System / Game Gear',
    defaultCore: 'genesis_plus_gx',
    selectedCore: 'genesis_plus_gx',
    availableCores: ['genesis_plus_gx', 'picodrive', 'gearsystem'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'segaMS',
  },
  {
    platformKey: 'Sega CD',
    platformName: 'Sega CD / Mega-CD & 32X',
    defaultCore: 'genesis_plus_gx',
    selectedCore: 'genesis_plus_gx',
    availableCores: ['genesis_plus_gx', 'picodrive'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'segaCD',
  },
  {
    platformKey: 'Sega Saturn',
    platformName: 'Sega Saturn',
    defaultCore: 'yabause',
    selectedCore: 'yabause',
    availableCores: ['yabause', 'beetle_saturn', 'kronos'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'segaSaturn',
  },
  {
    platformKey: 'PlayStation',
    platformName: 'Sony PlayStation (PS1)',
    defaultCore: 'mednafen_psx_hw',
    selectedCore: 'mednafen_psx_hw',
    availableCores: [
      'mednafen_psx_hw',
      'pcsx_rearmed',
      'swanstation',
      'mednafen_psx',
    ],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'psx',
  },
  {
    platformKey: 'PSP',
    platformName: 'Sony PlayStation Portable (PSP)',
    defaultCore: 'ppsspp',
    selectedCore: 'ppsspp',
    availableCores: ['ppsspp'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'psp',
  },
  {
    platformKey: 'TurboGrafx-16',
    platformName: 'NEC TurboGrafx-16 / PC Engine',
    defaultCore: 'mednafen_pce_fast',
    selectedCore: 'mednafen_pce_fast',
    availableCores: ['mednafen_pce_fast', 'mednafen_supergrafx'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'pce',
  },
  {
    platformKey: 'Neo Geo',
    platformName: 'SNK Neo Geo / Arcade',
    defaultCore: 'fbneo',
    selectedCore: 'fbneo',
    availableCores: ['fbneo', 'mame2003_plus', 'neocd'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'arcade',
  },
  {
    platformKey: 'Atari 2600',
    platformName: 'Atari 2600 / 7800 / Lynx / Jaguar',
    defaultCore: 'stella',
    selectedCore: 'stella',
    availableCores: ['stella', 'prosystem', 'handy', 'virtualjaguar'],
    supportsWebAssembly: true,
    emulatorJsCoreName: 'atari2600',
  },
  {
    platformKey: 'PlayStation 2',
    platformName: 'Sony PlayStation 2 (PS2)',
    defaultCore: 'pcsx2',
    selectedCore: 'pcsx2',
    availableCores: ['pcsx2', 'play'],
    supportsWebAssembly: false,
    emulatorJsCoreName: 'ps2',
  },
  {
    platformKey: 'GameCube',
    platformName: 'Nintendo GameCube / Wii',
    defaultCore: 'dolphin',
    selectedCore: 'dolphin',
    availableCores: ['dolphin'],
    supportsWebAssembly: false,
    emulatorJsCoreName: 'dolphin',
  },
];

const launchModeOptions: SelectInputOption[] = [
  {
    key: 'web',
    value: 'In-Browser Libretro Web Player (WebAssembly / EmulatorJS)',
  },
  {
    key: 'retroarch',
    value: 'Local RetroArch Desktop Process',
  },
  {
    key: 'protocol',
    value: 'Desktop URI Protocol Handler (controllarr://play)',
  },
];

const shaderOptions: SelectInputOption[] = [
  { key: 'none', value: 'None (Crisp Raw Pixels)' },
  { key: 'crt-mattias', value: 'CRT-Mattias (Curved Trinitron Scanlines)' },
  { key: 'crt-easymode', value: 'CRT-EasyMode (Clean Aperture Grille)' },
  { key: 'crt-aperture', value: 'CRT-Aperture (Sharp PVM Phosphor)' },
  { key: 'crt-geom', value: 'CRT-Geom (Classic Arcade Monitor)' },
  { key: 'lcd-grid-v2', value: 'LCD-Grid v2 (Handheld Dot Matrix)' },
  { key: '2xScaleHQ', value: '2xScaleHQ (Smooth Pixel Upscaler)' },
  { key: 'sabr', value: 'SABR v3.0 (Vectorized Pixel Smoothing)' },
];

export function loadLocalLibretroConfig(): LibretroLauncherConfig {
  try {
    const raw = window.localStorage.getItem(LIBRETRO_STORAGE_KEY);

    if (raw) {
      return { ...DEFAULT_CONFIG, ...JSON.parse(raw) };
    }
  } catch {
    // ignore storage errors
  }

  return DEFAULT_CONFIG;
}

interface PlatformCoreCardProps {
  platform: LibretroPlatformCoreOption;
  activeCore: string;
  onCoreChange: (platformKey: string, coreValue: string) => void;
  onResetCore: (platformKey: string) => void;
}

function PlatformCoreCard({
  platform,
  activeCore,
  onCoreChange,
  onResetCore,
}: PlatformCoreCardProps) {
  const handleSelectChange = useCallback(
    ({ value }: InputChanged<string>) => {
      onCoreChange(platform.platformKey, String(value));
    },
    [onCoreChange, platform.platformKey]
  );

  const handleResetClick = useCallback(() => {
    onResetCore(platform.platformKey);
  }, [onResetCore, platform.platformKey]);

  const coreSelectOptions: SelectInputOption[] = platform.availableCores.map(
    (coreName) => ({
      key: coreName,
      value:
        coreName === platform.defaultCore ? `${coreName} (Default)` : coreName,
    })
  );

  const isDefaultCore = activeCore === platform.defaultCore;

  return (
    <div className={styles.coreCard}>
      <div className={styles.coreCardHeader}>
        <span className={styles.platformName}>{platform.platformName}</span>
        <Label
          kind={platform.supportsWebAssembly ? kinds.SUCCESS : kinds.INFO}
          size={sizes.SMALL}
        >
          {platform.supportsWebAssembly ? 'WASM + Desktop' : 'Desktop Only'}
        </Label>
      </div>

      <div className={styles.coreCardControls}>
        <div className={styles.coreSelectWrapper}>
          <SelectInput
            name={`core_${platform.platformKey}`}
            value={activeCore}
            values={coreSelectOptions}
            onChange={handleSelectChange}
          />
        </div>

        {isDefaultCore ? null : (
          <button
            type="button"
            className={styles.resetButton}
            title={`Reset to ${platform.defaultCore}`}
            onClick={handleResetClick}
          >
            Reset
          </button>
        )}
      </div>
    </div>
  );
}

function LibretroSettings() {
  const [config, setConfig] = useState<LibretroLauncherConfig>(
    loadLocalLibretroConfig
  );
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);

  const { data: remoteConfig } = useApiQuery<LibretroLauncherConfig>({
    path: '/gamelauncher/config',
  });

  const { data: remoteCores } = useApiQuery<LibretroPlatformCoreOption[]>({
    path: '/gamelauncher/cores',
  });

  useEffect(() => {
    if (remoteConfig) {
      setConfig((prev) => ({
        ...prev,
        ...remoteConfig,
        coreOverrides: {
          ...prev.coreOverrides,
          ...(remoteConfig.coreOverrides ?? {}),
        },
      }));
    }
  }, [remoteConfig]);

  const { mutate: saveRemoteConfig } = useApiMutation<
    LibretroLauncherConfig,
    LibretroLauncherConfig
  >({
    path: '/gamelauncher/config',
    method: 'POST',
    mutationOptions: {
      onSuccess: (saved) => {
        window.localStorage.setItem(
          LIBRETRO_STORAGE_KEY,
          JSON.stringify(saved)
        );
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

  const handleCoreOverrideChange = useCallback(
    (platformKey: string, coreValue: string) => {
      setConfig((prev) => ({
        ...prev,
        coreOverrides: {
          ...prev.coreOverrides,
          [platformKey]: coreValue,
        },
      }));
      setHasPendingChanges(true);
      setSavedNotice(false);
    },
    []
  );

  const handleResetCore = useCallback((platformKey: string) => {
    setConfig((prev) => {
      const nextOverrides = { ...prev.coreOverrides };
      delete nextOverrides[platformKey];

      return {
        ...prev,
        coreOverrides: nextOverrides,
      };
    });
    setHasPendingChanges(true);
    setSavedNotice(false);
  }, []);

  const handleSavePress = useCallback(() => {
    window.localStorage.setItem(LIBRETRO_STORAGE_KEY, JSON.stringify(config));
    saveRemoteConfig(config);
    setHasPendingChanges(false);
    setSavedNotice(true);
  }, [config, saveRemoteConfig]);

  const platformList =
    remoteCores && remoteCores.length > 0 ? remoteCores : FALLBACK_PLATFORMS;

  return (
    <SettingsPage
      title={translate('LibretroSettings')}
      showSave={true}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('Libretro')}
          />

          {savedNotice ? (
            <Alert kind={kinds.SUCCESS}>
              Libretro emulation settings and per-platform core assignments
              saved.
            </Alert>
          ) : null}

          <Form>
            <FieldSet
              legend="Emulation & Game Launch Behavior"
              caption="Configure how Controllarr launches downloaded ROMs in the browser or via local Libretro / RetroArch installations"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  Enable In-Browser Libretro Web Player{' '}
                  <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                    WebAssembly
                  </Label>
                </FormLabel>
                <FormInputHelpText text="Allow launching downloaded ROMs directly inside Controllarr using WebAssembly Libretro cores (EmulatorJS)" />
                <CheckInput
                  name="enableWebPlayer"
                  value={config.enableWebPlayer}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Preferred Game Launch Mode</FormLabel>
                <FormInputHelpText text="Choose what happens when clicking Play on a game or platform in your library" />
                <SelectInput
                  name="preferredLaunchMode"
                  value={config.preferredLaunchMode}
                  values={launchModeOptions}
                  onChange={handleTextChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Default CRT / LCD Shader Filter</FormLabel>
                <FormInputHelpText text="Default WebGL shader applied when booting games in the Libretro Web Player" />
                <SelectInput
                  name="defaultShader"
                  value={config.defaultShader}
                  values={shaderOptions}
                  onChange={handleTextChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Auto-Inject Verified Platform BIOS</FormLabel>
                <FormInputHelpText text="Automatically mount verified BIOS firmware from the platform's /bios folder (PS1, Sega CD, Saturn, GBA, FDS) when starting a Libretro core" />
                <CheckInput
                  name="autoInjectBios"
                  value={config.autoInjectBios}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  Persist Battery Saves (.srm / .sav) &amp; Save States
                </FormLabel>
                <FormInputHelpText text="Automatically store and restore battery-backed SRAM saves and emulator states across play sessions" />
                <CheckInput
                  name="persistBatterySaves"
                  value={config.persistBatterySaves}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Pass RetroAchievements Session to Player</FormLabel>
                <FormInputHelpText text="Forward your configured RetroAchievements credentials when launching supported Libretro cores" />
                <CheckInput
                  name="enableRetroAchievementsInPlayer"
                  value={config.enableRetroAchievementsInPlayer}
                  onChange={handleCheckChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Start Emulator in Fullscreen</FormLabel>
                <FormInputHelpText text="Automatically enter fullscreen when launching a game in Web Player or Desktop RetroArch (-f)" />
                <CheckInput
                  name="fullScreenOnLaunch"
                  value={config.fullScreenOnLaunch}
                  onChange={handleCheckChange}
                />
              </FormRow>
            </FieldSet>

            <FieldSet
              legend="Desktop RetroArch Integration"
              caption="Optional local paths for launching games directly into a native Desktop RetroArch installation on the host machine"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>RetroArch Executable Path</FormLabel>
                <FormInputHelpText text="Full path to retroarch.exe (Windows), RetroArch.app/Contents/MacOS/RetroArch (macOS), or /usr/bin/retroarch (Linux)" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="retroArchExecutablePath"
                  value={config.retroArchExecutablePath}
                  placeholder="e.g. C:\RetroArch-Win64\retroarch.exe or /usr/bin/retroarch"
                  onChange={handleTextChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Libretro Cores Directory</FormLabel>
                <FormInputHelpText text="Directory containing compiled *_libretro.dll / .dylib / .so core binaries" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="libretroCoresDirectory"
                  value={config.libretroCoresDirectory}
                  placeholder="e.g. C:\RetroArch-Win64\cores or ~/.config/retroarch/cores"
                  onChange={handleTextChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Custom RetroArch Config Path (Optional)</FormLabel>
                <FormInputHelpText text="Optional custom retroarch.cfg file passed via --config when launching desktop games" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="retroArchConfigPath"
                  value={config.retroArchConfigPath}
                  placeholder="e.g. C:\RetroArch-Win64\retroarch.cfg"
                  onChange={handleTextChange}
                />
              </FormRow>
            </FieldSet>

            <FieldSet
              legend="WebAssembly Core Asset Delivery"
              caption="Configure the CDN or self-hosted static directory serving compiled Libretro WebAssembly (.wasm) core bundles"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Libretro WebAssembly Data URL</FormLabel>
                <FormInputHelpText text="Base URL providing loader.js and compiled WebAssembly Libretro cores (defaults to official EmulatorJS stable CDN)" />
                <FormInput
                  type={inputTypes.TEXT}
                  name="emulatorJsDataPath"
                  value={config.emulatorJsDataPath}
                  placeholder="https://cdn.emulatorjs.org/stable/data/"
                  onChange={handleTextChange}
                />
              </FormRow>
            </FieldSet>

            <FieldSet
              legend="Per-Platform Libretro Core Assignments"
              caption="Select your preferred Libretro emulator core for each console and handheld platform"
            >
              <div className={styles.coreGrid}>
                {platformList.map((platform) => {
                  const activeCore =
                    config.coreOverrides?.[platform.platformKey] ||
                    platform.selectedCore ||
                    platform.defaultCore;

                  return (
                    <PlatformCoreCard
                      key={platform.platformKey}
                      platform={platform}
                      activeCore={activeCore}
                      onCoreChange={handleCoreOverrideChange}
                      onResetCore={handleResetCore}
                    />
                  );
                })}
              </div>
            </FieldSet>
          </Form>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default LibretroSettings;
