import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import ImportMode from './ImportMode';

export interface InteractiveImportOptions
  extends Omit<PageableOptions, 'pageSize'> {
  importMode: ImportMode;
}

export const COLUMNS = [
  {
    name: 'relativePath',
    label: 'Relative Path',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'series',
    label: 'Platform',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'season',
    label: 'Region',
    isVisible: true,
  },
  {
    name: 'games',
    label: 'Games',
    isVisible: true,
  },
  {
    name: 'quality',
    label: 'Quality',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'languages',
    label: 'Languages',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'size',
    label: 'Size',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'releaseType',
    label: 'Release Type',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: 'Custom Formats',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'indexerFlags',
    label: 'Indexer Flags',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'rejections',
    label: 'Rejections',
    isSortable: true,
    isVisible: true,
  },
];

const {
  useOptions,
  useOption,
  getOptions,
  getOption,
  setOptions,
  setOption,
  setSort,
} = createOptionsStore<InteractiveImportOptions>(
  'interactive_import_options_v3',
  () => {
    return {
      selectedFilterKey: 'all',
      sortKey: 'relativePath',
      sortDirection: 'ascending',
      importMode: 'move',
      columns: COLUMNS,
    };
  }
);

export const useInteractiveImportOptions = useOptions;
export const getInteractiveImportOptions = getOptions;
export const setInteractiveImportOptions = setOptions;
export const useInteractiveImportOption = useOption;
export const getInteractiveImportOption = getOption;
export const setInteractiveImportOption = setOption;
export const setInteractiveImportSort = setSort;
