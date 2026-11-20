import React from 'react';
import usePlatform from 'Platform/usePlatform';
import sortByProp from 'Utilities/Array/sortByProp';
import FilterBuilderRowValue, {
  FilterBuilderRowValueProps,
} from './FilterBuilderRowValue';

type PlatformFilterBuilderRowValueProps<T> = Omit<
  FilterBuilderRowValueProps<T, number, string>,
  'tagList'
>;

function PlatformFilterBuilderRowValue<T>(
  props: PlatformFilterBuilderRowValueProps<T>
) {
  const { data: allSeries = [] } = usePlatform();

  const tagList = allSeries
    .map((series) => ({ id: series.id, name: series.title }))
    .sort(sortByProp('name'));

  return <FilterBuilderRowValue {...props} tagList={tagList} />;
}

export default PlatformFilterBuilderRowValue;
