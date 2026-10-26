import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import Series from 'Platform/Platform';
import {
  getImportPlatformItems,
  removeImportPlatformItemByPath,
} from './importPlatformStore';

export const useImportPlatform = () => {
  const queryClient = useQueryClient();

  const { isPending, error, mutate } = useApiMutation<Series[], Series[]>({
    path: '/series/import',
    method: 'POST',
    mutationOptions: {
      onSuccess: (data, newSeries) => {
        queryClient.invalidateQueries({ queryKey: ['/rootFolder'] });
        queryClient.setQueryData<Series[]>(['/series'], (oldSeries) => {
          if (!oldSeries) {
            return data;
          }

          return [...oldSeries, ...data];
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

        // Make sure we have a selected series and the same series hasn't been added yet.
        if (
          selectedSeries &&
          !acc.some((a) => a.tvdbId === selectedSeries.tvdbId)
        ) {
          const newSeries: Series = {
            ...selectedSeries,
            monitored: item.monitor !== 'none',
            monitorNewItems: 'all',
            qualityProfileId: item.qualityProfileId || 1,
            path: item.path,
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
    [mutate]
  );

  return {
    isImporting: isPending,
    importError: error,
    importPlatform,
  };
};
