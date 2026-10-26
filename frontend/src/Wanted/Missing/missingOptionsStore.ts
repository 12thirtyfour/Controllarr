import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import translate from 'Utilities/String/translate';

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<PageableOptions>('missing_options', () => {
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

export const useMissingOptions = useOptions;
export const setMissingOptions = setOptions;
export const useMissingOption = useOption;
export const setMissingOption = setOption;
export const setMissingSort = setSort;
