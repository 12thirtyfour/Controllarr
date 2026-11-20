import translate from 'Utilities/String/translate';

export default function getFinaleTypeName(finaleType?: string): string | null {
  if (!finaleType) {
    return null;
  }

  switch (finaleType) {
    case 'series':
    case 'season':
    case 'midseason':
      return translate('RetailRelease');
    default:
      return finaleType;
  }
}
