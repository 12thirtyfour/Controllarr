import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import translate from 'Utilities/String/translate';

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<PageableOptions>('cutoffUnmet_options_v2', () => {
    return {
      pageSize: 20,
      selectedFilterKey: 'monitored',
      sortKey: 'games.airDateUtc',
      sortDirection: 'descending',
      columns: [
        {
          name: 'series.sortTitle',
          label: () => translate('PlatformTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'game',
          label: () => translate('Game'),
          isVisible: true,
        },
        {
          name: 'games.title',
          label: () => translate('GameTitle'),
          isVisible: true,
        },
        {
          name: 'games.airDateUtc',
          label: () => translate('AirDate'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'games.lastSearchTime',
          label: () => translate('LastSearched'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isVisible: false,
        },
        {
          name: 'format',
          label: () => translate('Format'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'status',
          label: () => translate('Status'),
          isVisible: true,
        },
        {
          name: 'actions',
          label: '',
          columnLabel: () => translate('Actions'),
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
      ],
    };
  });

export const useCutoffUnmetOptions = useOptions;
export const setCutoffUnmetOptions = setOptions;
export const useCutoffUnmetOption = useOption;
export const setCutoffUnmetOption = setOption;
export const setCutoffUnmetSort = setSort;
