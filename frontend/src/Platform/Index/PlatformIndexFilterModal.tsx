import React, { useCallback } from 'react';
import FilterModal, { FilterModalProps } from 'Components/Filter/FilterModal';
import Series from 'Platform/Platform';
import { setSeriesOption } from 'Platform/platformOptionsStore';
import usePlatform, { FILTER_BUILDER } from 'Platform/usePlatform';

type PlatformIndexFilterModalProps = FilterModalProps<Series>;

export default function PlatformIndexFilterModal(
  props: PlatformIndexFilterModalProps
) {
  const { data: sectionItems } = usePlatform();

  const dispatchSetFilter = useCallback(
    ({ selectedFilterKey }: { selectedFilterKey: string | number }) => {
      setSeriesOption('selectedFilterKey', selectedFilterKey);
    },
    []
  );

  return (
    <FilterModal
      {...props}
      sectionItems={sectionItems}
      filterBuilderProps={FILTER_BUILDER}
      customFilterType="series"
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
