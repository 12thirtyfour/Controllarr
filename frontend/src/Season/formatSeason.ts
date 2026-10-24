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

  if (normalized.includes('nintendo-entertainment-system') && !normalized.includes('super')) {
    return seasonNumber === 3 ? 'Famicom' : null;
  }

  if (normalized.includes('super-nintendo')) {
    return seasonNumber === 3 ? 'Super Famicom' : null;
  }

  if (normalized.includes('genesis') || normalized.includes('mega-drive')) {
    return seasonNumber === 2 || seasonNumber === 3 ? 'Mega Drive' : null;
  }

  if (normalized.includes('turbografx-16') || normalized.includes('pc-engine')) {
    return seasonNumber === 3 ? 'PC Engine' : null;
  }

  return null;
}

export default function formatSeason(
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
    return shortFormat
      ? 'USA'
      : regionalConsole
        ? `USA (NTSC-U — ${regionalConsole})`
        : 'USA (NTSC-U)';
  }

  if (seasonNumber === 2) {
    return shortFormat
      ? 'EUR'
      : regionalConsole
        ? `Europe (PAL — ${regionalConsole})`
        : 'Europe (PAL)';
  }

  if (seasonNumber === 3) {
    return shortFormat
      ? 'JPN'
      : regionalConsole
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
