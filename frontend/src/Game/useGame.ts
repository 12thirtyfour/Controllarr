import { QueryKey, useQueryClient } from '@tanstack/react-query';
import { create } from 'zustand';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { PagedQueryResponse } from 'Helpers/Hooks/usePagedApiQuery';
import { CalendarItem } from 'typings/Calendar';
import Game from './Game';

export type GameEntity =
  | 'calendar'
  | 'games'
  | 'interactiveImport.games'
  | 'wanted.cutoffUnmet'
  | 'wanted.missing';

interface GameQueryKeyStore {
  calendar: QueryKey | null;
  games: QueryKey | null;
  cutoffUnmet: QueryKey | null;
  missing: QueryKey | null;
}

const gameQueryKeyStore = create<GameQueryKeyStore>(() => ({
  calendar: null,
  games: null,
  cutoffUnmet: null,
  missing: null,
}));

export const getQueryKey = (gameEntity: GameEntity) => {
  switch (gameEntity) {
    case 'calendar':
      return gameQueryKeyStore.getState().calendar;
    case 'games':
      return gameQueryKeyStore.getState().games;
    case 'wanted.cutoffUnmet':
      return gameQueryKeyStore.getState().cutoffUnmet;
    case 'wanted.missing':
      return gameQueryKeyStore.getState().missing;
    default:
      return null;
  }
};

export const setGameQueryKey = (
  gameEntity: GameEntity,
  queryKey: QueryKey | null
) => {
  switch (gameEntity) {
    case 'calendar':
      gameQueryKeyStore.setState({ calendar: queryKey });
      break;
    case 'games':
      gameQueryKeyStore.setState({ games: queryKey });
      break;
    case 'wanted.cutoffUnmet':
      gameQueryKeyStore.setState({ cutoffUnmet: queryKey });
      break;
    case 'wanted.missing':
      gameQueryKeyStore.setState({ missing: queryKey });
      break;
    default:
      break;
  }
};

const useGame = (
  gameId: number | undefined,
  gameEntity: GameEntity
) => {
  const queryClient = useQueryClient();
  const queryKey = getQueryKey(gameEntity);

  if (gameEntity === 'calendar') {
    return queryKey
      ? queryClient
          .getQueryData<CalendarItem[]>(queryKey)
          ?.find((e) => e.id === gameId)
      : undefined;
  }

  if (gameEntity === 'games') {
    return queryKey
      ? queryClient
          .getQueryData<Game[]>(queryKey)
          ?.find((e) => e.id === gameId)
      : undefined;
  }

  if (
    gameEntity === 'wanted.cutoffUnmet' ||
    gameEntity === 'wanted.missing'
  ) {
    return queryKey
      ? queryClient
          .getQueryData<PagedQueryResponse<Game>>(queryKey)
          ?.records?.find((e) => e.id === gameId)
      : undefined;
  }

  return undefined;
};

export default useGame;

interface ToggleGamesMonitored {
  gameIds: number[];
  monitored: boolean;
}

export const useToggleGamesMonitored = (queryKey: QueryKey) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, variables } = useApiMutation<
    unknown,
    ToggleGamesMonitored
  >({
    path: '/episode/monitor',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (_data, variables) => {
        queryClient.setQueryData<Game[] | undefined>(
          queryKey,
          (oldGames) => {
            if (!oldGames) {
              return oldGames;
            }

            return oldGames.map((oldGame) => {
              if (variables.gameIds.includes(oldGame.id)) {
                return {
                  ...oldGame,
                  monitored: variables.monitored,
                };
              }

              return oldGame;
            });
          }
        );
      },
    },
  });

  return {
    toggleGamesMonitored: mutate,
    isToggling: isPending,
    togglingGameIds: variables?.gameIds ?? [],
    togglingMonitored: variables?.monitored,
  };
};

const DEFAULT_EPISODES: Game[] = [];

export const useGamesWithIds = (gameIds: number[]) => {
  const queryClient = useQueryClient();
  const queryKey = getQueryKey('games');

  return queryKey
    ? queryClient
        .getQueryData<Game[]>(queryKey)
        ?.filter((e) => gameIds.includes(e.id)) ?? DEFAULT_EPISODES
    : DEFAULT_EPISODES;
};
