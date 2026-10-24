import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';

export default function formatSeason(
  seasonNumber: number,
  shortFormat?: boolean
) {
  if (seasonNumber === 0) {
    return shortFormat ? 'PROTO' : 'Prototypes, Betas & Unlicensed';
  }

  if (seasonNumber === 1) {
    return shortFormat ? 'USA' : 'USA (NTSC-U)';
  }

  if (seasonNumber === 2) {
    return shortFormat ? 'EUR' : 'Europe (PAL)';
  }

  if (seasonNumber === 3) {
    return shortFormat ? 'JPN' : 'Japan (NTSC-J)';
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
