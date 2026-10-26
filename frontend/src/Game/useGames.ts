import { useEffect, useMemo } from 'react';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import Game from './Game';
import { useGameOptions } from './gameOptionsStore';
import { setGameQueryKey } from './useGame';

const DEFAULT_EPISODES: Game[] = [];

interface SeriesGames {
  seriesId: number;
}

interface SeasonGames {
  seriesId: number | undefined;
  seasonNumber: number | undefined;
  isSelection: boolean;
}

interface GameIds {
  gameIds: number[];
}

interface RomFileId {
  romFileId: number;
}

export type GameFilter =
  | SeriesGames
  | SeasonGames
  | GameIds
  | RomFileId;

const useGames = (params: GameFilter) => {
  const setQueryKey = !('isSelection' in params);

  const { isPlaceholderData, queryKey, ...result } = useApiQuery<Game[]>({
    path: '/episode',
    queryParams:
      'isSelection' in params
        ? {
            seriesId: params.seriesId,
            seasonNumber: params.seasonNumber,
          }
        : { ...params },
    queryOptions: {
      enabled:
        ('seriesId' in params && params.seriesId !== undefined) ||
        ('gameIds' in params && params.gameIds?.length > 0) ||
        ('romFileId' in params && params.romFileId !== undefined),
    },
  });

  useEffect(() => {
    if (setQueryKey && !isPlaceholderData) {
      setGameQueryKey('games', queryKey);
    }
  }, [setQueryKey, isPlaceholderData, queryKey]);

  return {
    ...result,
    queryKey,
    data: result.data ?? DEFAULT_EPISODES,
  };
};

export default useGames;

export const useRegionGames = (seriesId: number, seasonNumber: number) => {
  const { data, ...result } = useGames({ seriesId });
  const { sortKey, sortDirection } = useGameOptions();

  const seasonGames = useMemo(() => {
    const { data: seasonGames } = clientSideFilterAndSort(
      data.filter((game) => game.seasonNumber === seasonNumber),
      {
        sortKey,
        sortDirection,
      }
    );

    return seasonGames;
  }, [data, seasonNumber, sortKey, sortDirection]);

  return {
    ...result,
    data: seasonGames,
  };
};
