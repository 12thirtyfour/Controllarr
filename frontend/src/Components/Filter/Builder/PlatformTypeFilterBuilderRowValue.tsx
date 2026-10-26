import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue, {
  FilterBuilderRowValueProps,
} from './FilterBuilderRowValue';

const seriesTypeList = [
  {
    id: 'anime',
    get name() {
      return translate('Anime');
    },
  },
  {
    id: 'daily',
    get name() {
      return translate('Daily');
    },
  },
  {
    id: 'standard',
    get name() {
      return translate('Standard');
    },
  },
];

type PlatformTypeFilterBuilderRowValueProps<T> = Omit<
  FilterBuilderRowValueProps<T, string, string>,
  'tagList'
>;

function PlatformTypeFilterBuilderRowValue<T>(
  props: PlatformTypeFilterBuilderRowValueProps<T>
) {
  return <FilterBuilderRowValue tagList={seriesTypeList} {...props} />;
}

export default PlatformTypeFilterBuilderRowValue;
