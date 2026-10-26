import { useMemo } from 'react';
import usePlatform from 'Platform/usePlatform';

function useExistingPlatform(tvdbId: number | undefined) {
  const { data: series = [] } = usePlatform();

  return useMemo(() => {
    if (tvdbId == null) {
      return false;
    }

    return series.some((s) => s.tvdbId === tvdbId);
  }, [tvdbId, series]);
}

export default useExistingPlatform;
