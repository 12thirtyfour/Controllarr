import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { useAddPlatformOptions } from 'AddPlatform/addPlatformOptionsStore';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import Series from 'Platform/Platform';
import useRootFolders from 'RootFolder/useRootFolders';
import {
  getImportPlatformItems,
  removeImportPlatformItemByPath,
} from './importPlatformStore';

export const useImportPlatform = () => {
  const queryClient = useQueryClient();
  const defaults = useAddPlatformOptions();
  const { data: targetRootFolders = [] } = useRootFolders();
  const primaryRootFolder = defaults.rootFolderPath || targetRootFolders[0]?.path || '';

  const { isPending, error, mutate } = useApiMutation<Series[], Series[]>({
    path: '/series/import',
    method: 'POST',
    mutationOptions: {
      onSuccess: (data, newSeries) => {
        queryClient.invalidateQueries({ queryKey: ['/importfolder'] });
        queryClient.invalidateQueries({ queryKey: ['/rootFolder'] });
        queryClient.setQueryData<Series[]>(['/series'], (oldSeries) => {
          if (!oldSeries) {
            return data;
          }

          const existingIds = new Set(data.map((d) => d.id));
          return [...oldSeries.filter((s) => !existingIds.has(s.id)), ...data];
        });

        newSeries.forEach((series) => {
          removeImportPlatformItemByPath(series.path);
        });
      },
    },
  });

  const importPlatform = useCallback(
    (ids: string[]) => {
      const items = getImportPlatformItems(ids);
      const addedIds: string[] = [];

      const allNewSeries = ids.reduce<Series[]>((acc, id) => {
        const item = items.find((i) => i.id === id);
        const selectedSeries = item?.selectedSeries;

        if (selectedSeries && item) {
          const isLeave = item.importMode === 'leave';
          const targetRoot = isLeave ? undefined : (item.rootFolderPath || primaryRootFolder);

          const newSeries: Series = {
            ...selectedSeries,
            monitored: item.monitor !== 'none',
            monitorNewItems: 'all',
            qualityProfileId: item.qualityProfileId || 1,
            path: item.path,
            rootFolderPath: targetRoot || '',
            seriesType: item.seriesType,
            language: item.language,
            seasonType: item.seasonType,
            seasonFolder: true,
            collectionStrategy: item.collectionStrategy || 'oneGameOneRom',
            preferredArchiveFormat: item.enableCompression ? 'auto' : 'none',
            addOptions: {
              monitor: item.monitor,
              searchForMissingGames: false,
              searchForCutoffUnmetGames: false,
              importMode: item.importMode || defaults.importMode || 'move',
            },
            tags: [],
          };

          newSeries.path = item.path;

          addedIds.push(id);
          acc.push(newSeries);
        }

        return acc;
      }, []);

      if (allNewSeries.length > 0) {
        mutate(allNewSeries);
      }
    },
    [defaults.importMode, mutate, primaryRootFolder]
  );

  return {
    isImporting: isPending,
    importError: error,
    importPlatform,
  };
};
