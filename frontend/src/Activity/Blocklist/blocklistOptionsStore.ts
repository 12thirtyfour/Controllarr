import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import translate from 'Utilities/String/translate';

export type BlocklistOptions = PageableOptions;

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<BlocklistOptions>('blocklist_options_v2', () => {
    return {
      pageSize: 20,
      selectedFilterKey: 'all',
      sortKey: 'time',
      sortDirection: 'descending',
      columns: [
        {
          name: 'series.sortTitle',
          label: () => translate('PlatformTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'sourceTitle',
          label: () => translate('SourceTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isVisible: false,
        },
        {
          name: 'protocol',
          label: () => translate('Source'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'date',
          label: () => translate('Date'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'indexer',
          label: () => translate('Indexer'),
          isSortable: true,
          isVisible: false,
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

export const useBlocklistOptions = useOptions;
export const setBlocklistOptions = setOptions;
export const useBlocklistOption = useOption;
export const setBlocklistOption = setOption;
export const setBlocklistSort = setSort;
