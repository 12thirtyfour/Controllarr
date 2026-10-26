import ReleaseType from 'InteractiveImport/ReleaseType';
import translate from 'Utilities/String/translate';

export default function getReleaseTypeName(
  releaseType?: ReleaseType
): string | null {
  switch (releaseType) {
    case 'singleGame':
      return translate('SingleGame');
    case 'multiGame':
      return translate('MultiGame');
    case 'seasonPack':
      return translate('SeasonPack');
    default:
      return translate('Unknown');
  }
}
