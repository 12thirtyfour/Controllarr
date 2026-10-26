import { useQueryClient } from '@tanstack/react-query';
import { GameEntity, getQueryKey } from 'Game/useGame';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { RomFile } from './RomFile';

const DEFAULT_ROM_FILES: RomFile[] = [];

interface PlatformRomFiles {
  seriesId: number;
}

interface RomFileIds {
  romFileIds?: number[];
  episodeFileIds?: number[];
}

export type RomFileFilter = PlatformRomFiles | RomFileIds;

const useRomFiles = (params: RomFileFilter) => {
  const ids = ('romFileIds' in params && params.romFileIds) || ('episodeFileIds' in params && params.episodeFileIds);
  const result = useApiQuery<RomFile[]>({
    path: '/episodeFile',
    queryParams: {
      ...params,
      episodeFileIds: ids || undefined,
    },
    queryOptions: {
      enabled:
        ('seriesId' in params && params.seriesId !== undefined) ||
        (Array.isArray(ids) && ids.length > 0),
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_ROM_FILES,
    hasRomFiles: !!result.data?.length,
  };
};

export default useRomFiles;

export const useDeleteRomFile = (
  id: number,
  gameEntity: GameEntity
) => {
  const queryClient = useQueryClient();

  const { mutate, error, isPending } = useApiMutation<unknown, void>({
    path: `/episodeFile/${id}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/episodeFile'] });
        queryClient.invalidateQueries({
          queryKey: [getQueryKey(gameEntity)],
        });
      },
    },
  });

  return {
    deleteRomFile: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

export const useDeleteRomFiles = () => {
  const queryClient = useQueryClient();

  const { mutate, error, isPending } = useApiMutation<unknown, RomFileIds>({
    path: '/episodeFile/bulk',
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/episodeFile'] });
        queryClient.invalidateQueries({ queryKey: ['/episode'] });
      },
    },
  });

  return {
    deleteRomFiles: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

export const useUpdateRomFiles = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    unknown,
    Partial<RomFile>[]
  >({
    path: '/episodeFile/bulk',
    method: 'PUT',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/episodeFile'] });
      },
    },
  });

  return {
    updateRomFiles: mutate,
    isUpdating: isPending,
    updateError: error,
  };
};
