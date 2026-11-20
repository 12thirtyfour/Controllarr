import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useReducer,
  useState,
} from 'react';
import {
  ConsoleThemeBase,
  ThemeName,
  useUiSettingsValues,
} from 'Settings/UI/useUiSettings';

const PLATFORM_THEME_MATCHING_KEY = 'controllarr_platform_theme_matching';

const LIGHT_DEFAULT_CONSOLES = new Set<ConsoleThemeBase>([
  'dreamcast',
  'wii',
  'xbox360',
]);

let previewThemeOverride: ThemeName | null = null;
let activePlatformThemeOverride: ConsoleThemeBase | null = null;
const themeListeners = new Set<() => void>();

export function getThemePolarity(
  theme: ThemeName,
  systemIsDark: boolean
): 'dark' | 'light' {
  if (theme === 'auto') {
    return systemIsDark ? 'dark' : 'light';
  }

  if (theme === 'dark' || theme.endsWith('-dark')) {
    return 'dark';
  }

  if (theme === 'light' || theme.endsWith('-light')) {
    return 'light';
  }

  return LIGHT_DEFAULT_CONSOLES.has(theme as ConsoleThemeBase)
    ? 'light'
    : 'dark';
}

function notifyThemeListeners() {
  themeListeners.forEach((listener) => listener());
}

export function setPreviewTheme(theme: ThemeName | null) {
  previewThemeOverride = theme;
  notifyThemeListeners();
}

export function setActivePlatformTheme(theme: ConsoleThemeBase | null) {
  if (activePlatformThemeOverride !== theme) {
    activePlatformThemeOverride = theme;
    notifyThemeListeners();
  }
}

export function getPlatformThemeMatchingEnabled(): boolean {
  try {
    return localStorage.getItem(PLATFORM_THEME_MATCHING_KEY) === 'true';
  } catch {
    return false;
  }
}

export function setPlatformThemeMatchingEnabled(enabled: boolean) {
  try {
    localStorage.setItem(PLATFORM_THEME_MATCHING_KEY, String(enabled));
  } catch {
    // Ignore storage error
  }

  notifyThemeListeners();
}

export function usePlatformThemeMatching(): [boolean, (val: boolean) => void] {
  const [enabled, setEnabled] = useState<boolean>(() =>
    getPlatformThemeMatchingEnabled()
  );

  useEffect(() => {
    const sync = () => setEnabled(getPlatformThemeMatchingEnabled());
    themeListeners.add(sync);

    return () => {
      themeListeners.delete(sync);
    };
  }, []);

  const update = useCallback((val: boolean) => {
    setPlatformThemeMatchingEnabled(val);
    setEnabled(val);
  }, []);

  return [enabled, update];
}

export function resolveConsoleThemeFromSlug(
  slugOrTitle?: string | null
): ConsoleThemeBase | null {
  if (!slugOrTitle) {
    return null;
  }

  const key = slugOrTitle.toLowerCase().replace(/[^a-z0-9]/g, '');

  const directMap: Record<string, ConsoleThemeBase> = {
    '32x': '32x',
    sega32x: '32x',
    '3do': '3do',
    panasonic3do: '3do',
    '3ds': '3ds',
    nintendo3ds: '3ds',
    '64dd': '64dd',
    nintendo64dd: '64dd',
    atari2600: 'atari2600',
    '2600': 'atari2600',
    atari5200: 'atari5200',
    '5200': 'atari5200',
    atari7800: 'atari7800',
    '7800': 'atari7800',
    cdi: 'cdi',
    philipscdi: 'cdi',
    channelf: 'channelf',
    fairchildchannelf: 'channelf',
    colecovision: 'colecovision',
    coleco: 'colecovision',
    dmg: 'dmg',
    gb: 'dmg',
    gameboy: 'dmg',
    dreamcast: 'dreamcast',
    segadreamcast: 'dreamcast',
    famicom: 'famicom',
    fds: 'fds',
    famicomdisksystem: 'fds',
    gamecube: 'gamecube',
    gcn: 'gamecube',
    ngc: 'gamecube',
    nintendogamecube: 'gamecube',
    gba: 'gba',
    gameboyadvance: 'gba',
    gbc: 'gbc',
    gameboycolor: 'gbc',
    genesis: 'genesis',
    segagenesis: 'genesis',
    gg: 'gg',
    gamegear: 'gg',
    segagamegear: 'gg',
    intellivision: 'intellivision',
    mattelintellivision: 'intellivision',
    intv: 'intellivision',
    jaguar: 'jaguar',
    atarijaguar: 'jaguar',
    jaguarcd: 'jaguarcd',
    atarijaguarcd: 'jaguarcd',
    lynx: 'lynx',
    atarilynx: 'lynx',
    megadrive: 'megadrive',
    segamegadrive: 'megadrive',
    n64: 'n64',
    nintendo64: 'n64',
    nds: 'nds',
    nintendods: 'nds',
    neogeo: 'neogeo',
    snkneogeo: 'neogeo',
    nes: 'nes',
    nintendoentertainmentsystem: 'nes',
    ngpc: 'ngpc',
    neogeopocketcolor: 'ngpc',
    odyssey2: 'odyssey2',
    magnavoxodyssey2: 'odyssey2',
    pcfx: 'pcfx',
    necpcfx: 'pcfx',
    pokemonmini: 'pokemonmini',
    pokemini: 'pokemonmini',
    ps1: 'ps1',
    psx: 'ps1',
    playstation: 'ps1',
    sonyplaystation: 'ps1',
    ps2: 'ps2',
    playstation2: 'ps2',
    sonyplaystation2: 'ps2',
    ps3: 'ps3',
    playstation3: 'ps3',
    sonyplaystation3: 'ps3',
    psp: 'psp',
    playstationportable: 'psp',
    psvita: 'psvita',
    playstationvita: 'psvita',
    satellaview: 'satellaview',
    bsx: 'satellaview',
    saturn: 'saturn',
    segasaturn: 'saturn',
    segacd: 'segacd',
    megacd: 'segacd',
    sms: 'sms',
    segamastersystem: 'sms',
    mastersystem: 'sms',
    snes: 'snes',
    supernintendo: 'snes',
    supernintendoentertainmentsystem: 'snes',
    superfamicom: 'superfamicom',
    sfc: 'superfamicom',
    switch: 'switch',
    nintendoswitch: 'switch',
    tg16: 'tg16',
    turbografx16: 'tg16',
    pcengine: 'tg16',
    tgcd: 'tgcd',
    turbografxcd: 'tgcd',
    pcenginecd: 'tgcd',
    vb: 'vb',
    virtualboy: 'vb',
    nintendovirtualboy: 'vb',
    vectrex: 'vectrex',
    wii: 'wii',
    nintendowii: 'wii',
    wiiu: 'wiiu',
    nintendowiiu: 'wiiu',
    wonderswan: 'wonderswan',
    wonderswancolor: 'wonderswan',
    wsc: 'wonderswan',
    xbox: 'xbox',
    microsoftxbox: 'xbox',
    xbox360: 'xbox360',
    microsoftxbox360: 'xbox360',
  };

  return directMap[key] ?? null;
}

const useTheme = (): Exclude<ThemeName, 'auto'> => {
  const { theme } = useUiSettingsValues();
  const [, forceUpdate] = useReducer((x: number) => x + 1, 0);
  const [systemIsDark, setSystemIsDark] = useState<boolean>(
    () => window.matchMedia('(prefers-color-scheme: dark)').matches
  );

  useEffect(() => {
    const onThemeChange = () => forceUpdate();
    themeListeners.add(onThemeChange);

    return () => {
      themeListeners.delete(onThemeChange);
    };
  }, []);

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const onSystemChange = (event: MediaQueryListEvent) => {
      setSystemIsDark(event.matches);
    };

    setSystemIsDark(mediaQuery.matches);
    mediaQuery.addEventListener('change', onSystemChange);

    return () => {
      mediaQuery.removeEventListener('change', onSystemChange);
    };
  }, []);

  const configuredTheme = (theme ??
    window.Controllarr.theme ??
    'auto') as ThemeName;
  const matchPlatform = getPlatformThemeMatchingEnabled();

  if (previewThemeOverride) {
    if (previewThemeOverride === 'auto') {
      return systemIsDark ? 'dark' : 'light';
    }

    return previewThemeOverride;
  }

  if (matchPlatform && activePlatformThemeOverride) {
    const polarity = getThemePolarity(configuredTheme, systemIsDark);
    return `${activePlatformThemeOverride}-${polarity}`;
  }

  if (configuredTheme === 'auto') {
    return systemIsDark ? 'dark' : 'light';
  }

  return configuredTheme;
};

export default useTheme;

const readThemeVar = (color: string) =>
  getComputedStyle(document.documentElement)
    .getPropertyValue(`--${color}`)
    .trim();

export const useThemeColor = (color: string) => {
  const theme = useTheme();
  const [value, setValue] = useState(() => readThemeVar(color));

  useLayoutEffect(() => {
    setValue(readThemeVar(color));
  }, [color, theme]);

  return value;
};
