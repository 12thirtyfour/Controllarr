import { useMemo } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import Series from 'Platform/Platform';
import usePlatform from 'Platform/usePlatform';
import sortByProp from 'Utilities/Array/sortByProp';

function useSelectedPlatformStats() {
  const { data: allSeries } = usePlatform();
  const { useSelectedIds } = useSelect<Series>();
  const seriesIds = useSelectedIds();

  const series = useMemo((): Series[] => {
    const seriesList = seriesIds.map((id) => {
      return allSeries.find((s) => s.id === id);
    }) as Series[];

    return seriesList.sort(sortByProp('sortTitle'));
  }, [allSeries, seriesIds]);

  const { totalRomFileCount, totalSizeOnDisk } = useMemo(() => {
    return series.reduce(
      (acc, { statistics = {} }) => {
        const { romFileCount = 0, sizeOnDisk = 0 } = statistics;

        acc.totalRomFileCount += romFileCount;
        acc.totalSizeOnDisk += sizeOnDisk;

        return acc;
      },
      {
        totalRomFileCount: 0,
        totalSizeOnDisk: 0,
      }
    );
  }, [series]);

  return {
    series,
    seriesIds,
    totalRomFileCount,
    totalSizeOnDisk,
  };
}

export default useSelectedPlatformStats;
