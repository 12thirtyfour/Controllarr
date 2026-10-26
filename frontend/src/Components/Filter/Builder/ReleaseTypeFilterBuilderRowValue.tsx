import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue, {
  FilterBuilderRowValueProps,
} from './FilterBuilderRowValue';

const releaseTypeList = [
  {
    id: 'unknown',
    get name() {
      return translate('Unknown');
    },
  },
  {
    id: 'singleGame',
    get name() {
      return translate('SingleGame');
    },
  },
  {
    id: 'multiGame',
    get name() {
      return translate('MultiGame');
    },
  },
  {
    id: 'seasonPack',
    get name() {
      return translate('SeasonPack');
    },
  },
];

type ReleaseTypeFilterBuilderRowValueProps<T> = Omit<
  FilterBuilderRowValueProps<T, string, string>,
  'tagList'
>;

function ReleaseTypeFilterBuilderRowValue<T>(
  props: ReleaseTypeFilterBuilderRowValueProps<T>
) {
  return <FilterBuilderRowValue tagList={releaseTypeList} {...props} />;
}

export default ReleaseTypeFilterBuilderRowValue;
