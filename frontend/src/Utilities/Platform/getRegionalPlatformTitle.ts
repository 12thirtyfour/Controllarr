export interface RegionalPlatformNames {
  US?: string;
  EU?: string;
  JP?: string;
}

const REGIONAL_CONSOLE_TITLES: Record<string, RegionalPlatformNames> = {
  // Nintendo
  nes: {
    US: 'Nintendo Entertainment System',
    EU: 'Nintendo Entertainment System',
    JP: 'Family Computer (Famicom)',
  },
  'nintendo-entertainment-system': {
    US: 'Nintendo Entertainment System',
    EU: 'Nintendo Entertainment System',
    JP: 'Family Computer (Famicom)',
  },
  snes: {
    US: 'Super Nintendo Entertainment System',
    EU: 'Super Nintendo Entertainment System',
    JP: 'Super Famicom',
  },
  'super-nintendo-entertainment-system': {
    US: 'Super Nintendo Entertainment System',
    EU: 'Super Nintendo Entertainment System',
    JP: 'Super Famicom',
  },
  'super-nintendo': {
    US: 'Super Nintendo Entertainment System',
    EU: 'Super Nintendo Entertainment System',
    JP: 'Super Famicom',
  },

  // Sega
  genesis: {
    US: 'Sega Genesis',
    EU: 'Sega Mega Drive',
    JP: 'Sega Mega Drive',
  },
  'sega-genesis': {
    US: 'Sega Genesis',
    EU: 'Sega Mega Drive',
    JP: 'Sega Mega Drive',
  },
  'mega-drive': {
    US: 'Sega Genesis',
    EU: 'Sega Mega Drive',
    JP: 'Sega Mega Drive',
  },
  'sega-mega-drive': {
    US: 'Sega Genesis',
    EU: 'Sega Mega Drive',
    JP: 'Sega Mega Drive',
  },
  sms: {
    US: 'Sega Master System',
    EU: 'Sega Master System',
    JP: 'Sega Mark III',
  },
  'sega-master-system': {
    US: 'Sega Master System',
    EU: 'Sega Master System',
    JP: 'Sega Mark III',
  },
  segacd: {
    US: 'Sega CD',
    EU: 'Mega-CD',
    JP: 'Mega-CD',
  },
  'sega-cd': {
    US: 'Sega CD',
    EU: 'Mega-CD',
    JP: 'Mega-CD',
  },
  '32x': {
    US: 'Sega 32X',
    EU: 'Mega Drive 32X',
    JP: 'Super 32X',
  },
  'sega-32x': {
    US: 'Sega 32X',
    EU: 'Mega Drive 32X',
    JP: 'Super 32X',
  },

  // NEC
  tg16: {
    US: 'TurboGrafx-16',
    EU: 'TurboGrafx',
    JP: 'PC Engine',
  },
  'turbografx-16': {
    US: 'TurboGrafx-16',
    EU: 'TurboGrafx',
    JP: 'PC Engine',
  },
  tgcd: {
    US: 'TurboGrafx-CD',
    EU: 'TurboGrafx-CD',
    JP: 'PC Engine CD-ROM²',
  },
  'turbografx-cd': {
    US: 'TurboGrafx-CD',
    EU: 'TurboGrafx-CD',
    JP: 'PC Engine CD-ROM²',
  },
  supergrafx: {
    US: 'SuperGrafx',
    EU: 'SuperGrafx',
    JP: 'PC Engine SuperGrafx',
  },
  'pc-engine-supergrafx': {
    US: 'SuperGrafx',
    EU: 'SuperGrafx',
    JP: 'PC Engine SuperGrafx',
  },

  // Magnavox / Philips
  'magnavox-odyssey-2': {
    US: 'Magnavox Odyssey 2',
    EU: 'Philips Videopac G7000',
    JP: 'Magnavox Odyssey 2',
  },
  'odyssey-2': {
    US: 'Magnavox Odyssey 2',
    EU: 'Philips Videopac G7000',
    JP: 'Magnavox Odyssey 2',
  },
};

export default function getRegionalPlatformTitle(
  platformSlugOrTitle: string,
  consoleRegion: string = 'US',
  defaultTitle?: string
): string {
  if (!platformSlugOrTitle) {
    return defaultTitle ?? '';
  }

  const normalized = platformSlugOrTitle
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

  const region = (consoleRegion || 'US').toUpperCase() as keyof RegionalPlatformNames;
  const match = REGIONAL_CONSOLE_TITLES[normalized];

  if (match && match[region]) {
    return match[region]!;
  }

  return defaultTitle ?? platformSlugOrTitle;
}
