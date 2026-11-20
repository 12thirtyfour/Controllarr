import React, { useCallback } from 'react';
import { SetFilter } from 'Components/Filter/Filter';
import FilterModal, { FilterModalProps } from 'Components/Filter/FilterModal';
import Game from 'Game/Game';
import { setCutoffUnmetOption } from './cutoffUnmetOptionsStore';
import useCutoffUnmet, { FILTER_BUILDER } from './useCutoffUnmet';

type CutoffUnmetFilterModalProps = FilterModalProps<Game>;

export default function CutoffUnmetFilterModal(
  props: CutoffUnmetFilterModalProps
) {
  const { records } = useCutoffUnmet();

  const dispatchSetFilter = useCallback(({ selectedFilterKey }: SetFilter) => {
    setCutoffUnmetOption('selectedFilterKey', selectedFilterKey);
  }, []);

  return (
    <FilterModal
      {...props}
      sectionItems={records}
      filterBuilderProps={FILTER_BUILDER}
      customFilterType="wanted.cutoffUnmet"
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
