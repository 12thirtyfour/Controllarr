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
  slugOrTitle?: string | null,
  consoleRegion?: string | null
): ConsoleThemeBase | null {
  if (!slugOrTitle) {
    return null;
  }

  const key = slugOrTitle.toLowerCase().replace(/[^a-z0-9]/g, '');

  const directMap: Record<string, ConsoleThemeBase> = {
    '32x': '32x',
    megadrive32x: '32x',
    sega32x: '32x',
    super32x: '32x',
    '3do': '3do',
    '3dointeractivemultiplayer': '3do',
    panasonic3do: '3do',
    the3docompany3do: '3do',
    '3ds': '3ds',
    n3ds: '3ds',
    nintendo3ds: '3ds',
    '3dseshop': '3dseshop',
    '3dsdigital': '3dseshop',
    eshop3ds: '3dseshop',
    nintendo3dseshop: '3dseshop',
    '64dd': '64dd',
    n64dd: '64dd',
    nintendo64dd: '64dd',
    adventurevision: 'adventurevision',
    entexadventurevision: 'adventurevision',
    arcadia2001: 'arcadia2001',
    emersonarcadia2001: 'arcadia2001',
    leisurevision: 'arcadia2001',
    arduboy: 'arduboy',
    arduboyincarduboy: 'arduboy',
    atari2600: 'atari2600',
    '2600': 'atari2600',
    vcs: 'atari2600',
    atari5200: 'atari5200',
    '5200': 'atari5200',
    a5200: 'atari5200',
    atari7800: 'atari7800',
    '7800': 'atari7800',
    beena: 'beena',
    advancedpicobeena: 'beena',
    segaadvancedpicobeena: 'beena',
    segabeena: 'beena',
    cassettevision: 'cassettevision',
    epochcassettevision: 'cassettevision',
    cd32: 'cd32',
    amigacd32: 'cd32',
    commodoreamigacd32: 'cd32',
    commodorecd32: 'cd32',
    cdi: 'cdi',
    philipscdi: 'cdi',
    channelf: 'channelf',
    fairchildchannelf: 'channelf',
    colecovision: 'colecovision',
    coleco: 'colecovision',
    colecocolecovision: 'colecovision',
    creativision: 'creativision',
    vtechcreativision: 'creativision',
    dmg: 'dmg',
    gb: 'dmg',
    gameboy: 'dmg',
    nintendogameboy: 'dmg',
    dreamcast: 'dreamcast',
    dc: 'dreamcast',
    segadreamcast: 'dreamcast',
    dsiware: 'dsiware',
    dsishop: 'dsiware',
    nintendodsishop: 'dsiware',
    nintendodsiware: 'dsiware',
    ereader: 'ereader',
    cardereader: 'ereader',
    nintendoereader: 'ereader',
    famicom: 'famicom',
    familycomputerfamicom: 'famicom',
    fds: 'fds',
    famicomdisksystem: 'fds',
    nintendofamicomdisksystem: 'fds',
    gameandwatch: 'gameandwatch',
    handheldelectronicgame: 'gameandwatch',
    nintendogameandwatch: 'gameandwatch',
    gamecom: 'gamecom',
    tigergamecom: 'gamecom',
    gamecube: 'gamecube',
    gcn: 'gamecube',
    ngc: 'gamecube',
    nintendogamecube: 'gamecube',
    gamemaster: 'gamemaster',
    hartunggamemaster: 'gamemaster',
    gba: 'gba',
    agb: 'gba',
    gameboyadvance: 'gba',
    nintendogameboyadvance: 'gba',
    gbc: 'gbc',
    cgb: 'gbc',
    gameboycolor: 'gbc',
    nintendogameboycolor: 'gbc',
    genesis: 'genesis',
    md: 'genesis',
    segagenesis: 'genesis',
    gg: 'gg',
    gamegear: 'gg',
    segagamegear: 'gg',
    gp32: 'gp32',
    gameparkgp32: 'gp32',
    gx4000: 'gx4000',
    amstradgx4000: 'gx4000',
    intellivision: 'intellivision',
    mattelintellivision: 'intellivision',
    intv: 'intellivision',
    jaguar: 'jaguar',
    atarijaguar: 'jaguar',
    jaguarcd: 'jaguarcd',
    atarijaguarcd: 'jaguarcd',
    leapster: 'leapster',
    leapfrogleapster: 'leapster',
    leapsterlearninggamesystem: 'leapster',
    loopy: 'loopy',
    casioloopy: 'loopy',
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
    neogeocd: 'neogeocd',
    ngcd: 'neogeocd',
    snkneogeocd: 'neogeocd',
    nes: 'nes',
    nintendoentertainmentsystem: 'nes',
    ngp: 'ngp',
    neogeopocket: 'ngp',
    snkneogeopocket: 'ngp',
    ngpc: 'ngpc',
    neogeopocketcolor: 'ngpc',
    snkneogeopocketcolor: 'ngpc',
    odyssey2: 'odyssey2',
    magnavoxodyssey2: 'odyssey2',
    philipsvideopacg7000: 'odyssey2',
    videopac: 'odyssey2',
    pcfx: 'pcfx',
    necpcfx: 'pcfx',
    picno: 'picno',
    konamipicno: 'picno',
    pico: 'pico',
    segapico: 'pico',
    pocketchallengev2: 'pocketchallengev2',
    benessepocketchallengev2: 'pocketchallengev2',
    pokemonmini: 'pokemonmini',
    nintendopokemonmini: 'pokemonmini',
    pokemini: 'pokemonmini',
    ps1: 'ps1',
    psone: 'ps1',
    psx: 'ps1',
    playstation: 'ps1',
    sonyplaystation: 'ps1',
    ps2: 'ps2',
    playstation2: 'ps2',
    sonyplaystation2: 'ps2',
    ps3: 'ps3',
    playstation3: 'ps3',
    sonyplaystation3: 'ps3',
    ps3psn: 'ps3psn',
    playstation3psn: 'ps3psn',
    sonyplaystation3psn: 'ps3psn',
    psp: 'psp',
    playstationportable: 'psp',
    sonyplaystationportable: 'psp',
    sonypsp: 'psp',
    psppsn: 'psppsn',
    playstationportablepsn: 'psppsn',
    sonyplaystationportablepsn: 'psppsn',
    psvita: 'psvita',
    playstationvita: 'psvita',
    psv: 'psvita',
    sonyplaystationvita: 'psvita',
    vita: 'psvita',
    pv1000: 'pv1000',
    casiopv1000: 'pv1000',
    satellaview: 'satellaview',
    bsx: 'satellaview',
    nintendosatellaview: 'satellaview',
    saturn: 'saturn',
    segasaturn: 'saturn',
    ss: 'saturn',
    scv: 'scv',
    epochsupercassettevision: 'scv',
    supercassettevision: 'scv',
    yenosupercassettevision: 'scv',
    segacd: 'segacd',
    megacd: 'segacd',
    scd: 'segacd',
    sg1000: 'sg1000',
    sc3000: 'sg1000',
    segasg1000: 'sg1000',
    sms: 'sms',
    segamastersystem: 'sms',
    segamarkiii: 'sms',
    mastersystem: 'sms',
    snes: 'snes',
    supernintendo: 'snes',
    supernintendoentertainmentsystem: 'snes',
    studio2: 'studio2',
    rcastudioii: 'studio2',
    studioii: 'studio2',
    sufamiturbo: 'sufamiturbo',
    bandaisufamiturbo: 'sufamiturbo',
    nintendosufamiturbo: 'sufamiturbo',
    superacan: 'superacan',
    funtechsuperacan: 'superacan',
    superfamicom: 'superfamicom',
    sfc: 'superfamicom',
    supergrafx: 'supergrafx',
    necpcenginesupergrafx: 'supergrafx',
    pcenginesupergrafx: 'supergrafx',
    sgx: 'supergrafx',
    supervision: 'supervision',
    watarasupervision: 'supervision',
    switch: 'switch',
    nintendoswitch: 'switch',
    nsw: 'switch',
    nx: 'switch',
    switcheshop: 'switcheshop',
    nintendoswitcheshop: 'switcheshop',
    nsweshop: 'switcheshop',
    switchdigital: 'switcheshop',
    tg16: 'tg16',
    turbografx16: 'tg16',
    necturbografx16: 'tg16',
    pcengine: 'tg16',
    pce: 'tg16',
    tgcd: 'tgcd',
    turbografxcd: 'tgcd',
    necturbografxcd: 'tgcd',
    pcenginecd: 'tgcd',
    pcecd: 'tgcd',
    vb: 'vb',
    virtualboy: 'vb',
    nintendovirtualboy: 'vb',
    vc4000: 'vc4000',
    intertonvc4000: 'vc4000',
    vectrex: 'vectrex',
    gcevectrex: 'vectrex',
    videopacplus: 'videopacplus',
    g7400: 'videopacplus',
    philipsvideopac: 'videopacplus',
    philipsvideopacg7400: 'videopacplus',
    videopacplusg7400: 'videopacplus',
    vsmile: 'vsmile',
    vtechvsmile: 'vsmile',
    wii: 'wii',
    nintendowii: 'wii',
    wiiu: 'wiiu',
    nintendowiiu: 'wiiu',
    wiiueshop: 'wiiueshop',
    nintendowiiueshop: 'wiiueshop',
    wiiudigital: 'wiiueshop',
    wiiware: 'wiiware',
    nintendowiiware: 'wiiware',
    wiishopchannel: 'wiiware',
    wonderswan: 'wonderswan',
    bandaiwonderswancolor: 'wonderswan',
    wonderswancolor: 'wonderswan',
    wsc: 'wonderswan',
    ws: 'ws',
    bandaiwonderswan: 'ws',
    xbox: 'xbox',
    microsoftxbox: 'xbox',
    ogxbox: 'xbox',
    originalxbox: 'xbox',
    xbox360: 'xbox360',
    microsoftxbox360: 'xbox360',
    x360: 'xbox360',
    xbox360digital: 'xbox360digital',
    microsoftxbox360digital: 'xbox360digital',
    xbla: 'xbox360digital',
    xboxlivearcade: 'xbox360digital',
    zeebo: 'zeebo',
    mobilezeebo: 'zeebo',
  };

  const resolved = directMap[key] ?? null;

  if (!resolved) {
    return null;
  }

  const region = consoleRegion?.trim().toUpperCase();

  if (resolved === 'nes' && region === 'JP') {
    return 'famicom';
  }

  if (resolved === 'snes' && region === 'JP') {
    return 'superfamicom';
  }

  if (resolved === 'genesis' && (region === 'EU' || region === 'JP')) {
    return 'megadrive';
  }

  return resolved;
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
