import React, {
  createContext,
  PropsWithChildren,
  useContext,
  useMemo,
} from 'react';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import Queue from 'typings/Queue';

interface GameDetails {
  gameIds: number[];
}

interface PlatformDetails {
  seriesId: number;
}

interface AllDetails {
  all: boolean;
}

type QueueDetailsFilter = AllDetails | GameDetails | PlatformDetails;

const QueueDetailsContext = createContext<ReadonlyArray<Queue> | undefined>(
  undefined
);

export default function QueueDetailsProvider({
  children,
  ...filter
}: PropsWithChildren<QueueDetailsFilter>) {
  const { data } = useApiQuery<Queue[]>({
    path: '/queue/details',
    queryParams: { ...filter },
    queryOptions: {
      enabled: Object.keys(filter).length > 0,
    },
  });

  return (
    <QueueDetailsContext.Provider value={data}>
      {children}
    </QueueDetailsContext.Provider>
  );
}

export function useQueueItemForGame(gameId: number) {
  const queue = useContext(QueueDetailsContext);

  return useMemo(() => {
    return queue?.find((item) => item.gameIds?.includes(gameId));
  }, [gameId, queue]);
}

export function useIsDownloadingGames(gameIds: number[]) {
  const queue = useContext(QueueDetailsContext);

  return useMemo(() => {
    if (!queue) {
      return false;
    }

    return queue.some((item) =>
      item.gameIds?.some((e) => gameIds.includes(e))
    );
  }, [gameIds, queue]);
}

export interface SeriesQueueDetails {
  count: number;
  gamesWithFiles: number;
}

export function useQueueDetailsForSeries(
  seriesId: number,
  seasonNumber?: number
) {
  const queue = useContext(QueueDetailsContext);

  return useMemo<SeriesQueueDetails>(() => {
    if (!queue) {
      return { count: 0, gamesWithFiles: 0 };
    }

    return queue.reduce<SeriesQueueDetails>(
      (acc: SeriesQueueDetails, item) => {
        if (
          item.trackedDownloadState === 'imported' ||
          item.seriesId !== seriesId
        ) {
          return acc;
        }

        if (
          seasonNumber != null &&
          !item.seasonNumbers?.includes(seasonNumber)
        ) {
          return acc;
        }

        acc.count += item.gameIds.length;
        acc.gamesWithFiles += item.gamesWithFilesCount;

        return acc;
      },
      {
        count: 0,
        gamesWithFiles: 0,
      }
    );
  }, [seriesId, seasonNumber, queue]);
}

export const useQueueDetails = () => {
  return useContext(QueueDetailsContext) ?? [];
};
