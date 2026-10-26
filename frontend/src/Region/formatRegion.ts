import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';

const REGIONAL_CONSOLE_NAMES: Record<string, Record<number, string>> = {
  'nintendo-entertainment-system': {
    3: 'Famicom',
  },
  nes: {
    3: 'Famicom',
  },
  'super-nintendo-entertainment-system': {
    3: 'Super Famicom',
  },
  snes: {
    3: 'Super Famicom',
  },
  'sega-master-system': {
    3: 'Sega Mark III',
  },
  'sega-genesis': {
    2: 'Mega Drive',
    3: 'Mega Drive',
  },
  genesis: {
    2: 'Mega Drive',
    3: 'Mega Drive',
  },
  'sega-cd': {
    2: 'Mega-CD',
    3: 'Mega-CD',
  },
  'sega-32x': {
    2: 'Mega Drive 32X',
    3: 'Super 32X',
  },
  'turbografx-16': {
    2: 'TurboGrafx',
    3: 'PC Engine',
  },
  'turbografx-cd': {
    3: 'PC Engine CD-ROM²',
  },
  'magnavox-odyssey-2': {
    2: 'Philips Videopac G7000',
  },
};

export function getRegionalConsoleName(
  seasonNumber: number,
  platformSlugOrTitle?: string
): string | null {
  if (!platformSlugOrTitle) {
    return null;
  }

  const normalized = platformSlugOrTitle
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

  const regionalMap = REGIONAL_CONSOLE_NAMES[normalized];

  if (regionalMap && regionalMap[seasonNumber]) {
    return regionalMap[seasonNumber];
  }

  if (
    normalized.includes('nintendo-entertainment-system') ||
    normalized.includes('famicom') ||
    normalized === 'nes'
  ) {
    if (seasonNumber === 1) return 'NES';
    if (seasonNumber === 3) return 'Famicom';
  }

  if (
    normalized.includes('super-nintendo') ||
    normalized.includes('super-famicom') ||
    normalized === 'snes'
  ) {
    if (seasonNumber === 1) return 'Super Nintendo';
    if (seasonNumber === 3) return 'Super Famicom';
  }

  if (normalized.includes('genesis') || normalized.includes('mega-drive')) {
    if (seasonNumber === 1) return 'Genesis';
    if (seasonNumber === 2 || seasonNumber === 3) return 'Mega Drive';
  }

  if (
    normalized.includes('master-system') ||
    normalized.includes('mark-iii') ||
    normalized === 'sms'
  ) {
    if (seasonNumber === 1 || seasonNumber === 2) return 'Master System';
    if (seasonNumber === 3) return 'Sega Mark III';
  }

  if (
    normalized.includes('turbografx-16') ||
    normalized.includes('pc-engine') ||
    normalized === 'tg16'
  ) {
    if (seasonNumber === 1) return 'TurboGrafx-16';
    if (seasonNumber === 2) return 'TurboGrafx';
    if (seasonNumber === 3) return 'PC Engine';
  }

  if (
    normalized.includes('turbografx-cd') ||
    normalized.includes('pc-engine-cd') ||
    normalized === 'tgcd'
  ) {
    if (seasonNumber === 1 || seasonNumber === 2) return 'TurboGrafx-CD';
    if (seasonNumber === 3) return 'PC Engine CD-ROM²';
  }

  return null;
}

export default function formatRegion(
  seasonNumber: number,
  shortFormat?: boolean,
  platformSlugOrTitle?: string
) {
  const regionalConsole = shortFormat
    ? null
    : getRegionalConsoleName(seasonNumber, platformSlugOrTitle);

  if (seasonNumber === 0) {
    return shortFormat ? 'UNL' : 'Unlicensed';
  }

  if (seasonNumber === 1) {
    if (shortFormat) {
      return 'USA';
    }

    return regionalConsole
      ? `USA (NTSC-U — ${regionalConsole})`
      : 'USA (NTSC-U)';
  }

  if (seasonNumber === 2) {
    if (shortFormat) {
      return 'EUR';
    }

    return regionalConsole
      ? `Europe (PAL — ${regionalConsole})`
      : 'Europe (PAL)';
  }

  if (seasonNumber === 3) {
    if (shortFormat) {
      return 'JPN';
    }

    return regionalConsole
      ? `Japan (NTSC-J — ${regionalConsole})`
      : 'Japan (NTSC-J)';
  }

  if (seasonNumber === 4) {
    return shortFormat ? 'WLD' : 'World / Multi-Region';
  }

  if (seasonNumber === 5) {
    return shortFormat ? 'HBW' : 'Homebrew & Aftermarket';
  }

  if (seasonNumber === 6) {
    return shortFormat ? 'HACK' : 'ROM Hacks & Translations';
  }

  if (seasonNumber > 0) {
    return shortFormat
      ? `R${padNumber(seasonNumber, 2)}`
      : translate('SeasonNumberToken', { seasonNumber });
  }

  return null;
}
