import { useEffect } from 'react';
import { create } from 'zustand';
import { useShallow } from 'zustand/react/shallow';
import {
  useAddPlatformLanguage,
  useAddPlatformOptions,
} from 'AddPlatform/addPlatformOptionsStore';
import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import Language from 'Language/Language';
import { UnmappedFolder } from 'RootFolder/useRootFolders';
import Series, { SeriesMonitor, SeriesType } from 'Platform/Platform';

interface ImportPlatformViewOptions {
  compactRows: boolean;
}

const viewOptions = createOptionsStore<ImportPlatformViewOptions>(
  'import_series_view_options',
  () => {
    return {
      compactRows: false,
    };
  }
);

export const useImportPlatformViewOption = viewOptions.useOption;
export const setImportPlatformViewOption = viewOptions.setOption;

export interface UnamppedFolderItem extends UnmappedFolder {
  id: string;
}

export interface ImportPlatformItem {
  id: string;
  language: Language;
  monitor: SeriesMonitor;
  path: string;
  qualityProfileId: number;
  relativePath: string;
  seasonFolder: boolean;
  seasonType: string;
  collectionStrategy: string;
  enableCompression: boolean;
  selectedSeries?: Series;
  seriesType: SeriesType;
  name: string;
  hasSearched: boolean;
}

interface ImportPlatformState {
  items: Record<string, ImportPlatformItem>;
  lookupQueue: string[];
  isProcessing: boolean;
}

const defaultState: ImportPlatformState = {
  items: {},
  lookupQueue: [],
  isProcessing: false,
};

const importPlatformStore = create<ImportPlatformState>()(() => defaultState);

export const useEnsureImportPlatformItems = (
  unmappedFolders: UnamppedFolderItem[]
) => {
  const {
    monitor,
    qualityProfileId,
    seriesType,
    collectionStrategy,
    enableCompression,
  } = useAddPlatformOptions();
  const language = useAddPlatformLanguage();

  useEffect(() => {
    unmappedFolders.forEach((unmappedFolder) => {
      const existingItem =
        importPlatformStore.getState().items[unmappedFolder.id];

      if (existingItem) {
        return;
      }

      const newItem: ImportPlatformItem = {
        ...unmappedFolder,
        language,
        monitor,
        qualityProfileId: qualityProfileId || 1,
        seriesType,
        seasonFolder: true,
        seasonType: 'official',
        collectionStrategy: collectionStrategy || 'oneGameOneRom',
        enableCompression: enableCompression ?? true,
        hasSearched: false,
      };

      importPlatformStore.setState((state) => ({
        items: {
          ...state.items,
          [unmappedFolder.id]: newItem,
        },
      }));
    });
  }, [
    unmappedFolders,
    language,
    monitor,
    qualityProfileId,
    seriesType,
    collectionStrategy,
    enableCompression,
  ]);
};

export const updateImportPlatformItem = (
  itemData: Partial<ImportPlatformItem> & Pick<ImportPlatformItem, 'id'>
) => {
  importPlatformStore.setState((state) => {
    const existingItem = state.items[itemData.id];

    if (existingItem) {
      return {
        items: {
          ...state.items,
          [itemData.id]: {
            ...existingItem,
            ...itemData,
          },
        },
      };
    }

    return state;
  });
};

export const removeImportPlatformItemByPath = (path: string) => {
  importPlatformStore.setState((state) => {
    const item = Object.values(state.items).find((i) => i.path === path);

    if (!item) {
      return state;
    }

    const { [item.id]: removed, ...items } = state.items;

    return { items };
  });
};

export const clearImportPlatform = () => {
  importPlatformStore.setState(defaultState);
};

export const startProcessing = () => {
  importPlatformStore.setState((state) => {
    const items = Object.values(state.items).reduce<string[]>((acc, item) => {
      if (!item.hasSearched) {
        acc.push(item.id);
      }

      return acc;
    }, []);

    return { isProcessing: true, lookupQueue: items };
  });
};

export const stopProcessing = () => {
  importPlatformStore.setState({ isProcessing: false, lookupQueue: [] });
};

export const addToLookupQueue = (id: string, addToStart = false) => {
  importPlatformStore.setState((state) => {
    if (state.lookupQueue.includes(id)) {
      return state;
    }

    return {
      lookupQueue: addToStart
        ? [id, ...state.lookupQueue]
        : [...state.lookupQueue, id],
    };
  });
};

export const removeFromLookupQueue = (id: string) => {
  importPlatformStore.setState((state) => ({
    lookupQueue: state.lookupQueue.filter((queuedId) => queuedId !== id),
  }));
};

export const useIsCurrentLookupQueueItem = (id: string) => {
  return importPlatformStore((state) => state.lookupQueue[0] === id);
};

export const useIsCurrentItemQueued = (id: string) => {
  return importPlatformStore((state) => state.lookupQueue.includes(id));
};

export const useHasSearchedItems = () => {
  return importPlatformStore((state) =>
    Object.values(state.items).some((item) => item.hasSearched)
  );
};

export const useLookupQueueHasItems = () => {
  return importPlatformStore((state) => state.lookupQueue.length > 0);
};

export const useImportPlatformItem = (id: string) => {
  return importPlatformStore((state) => state.items[id]);
};

export const useImportPlatformItems = () => {
  return importPlatformStore(useShallow((state) => Object.values(state.items)));
};

export const getImportPlatformItems = (ids: string[]) => {
  const state = importPlatformStore.getState();

  return ids.reduce<ImportPlatformItem[]>((acc, id) => {
    const item = state.items[id];

    if (item != null) {
      acc.push(item);
    }

    return acc;
  }, []);
};
