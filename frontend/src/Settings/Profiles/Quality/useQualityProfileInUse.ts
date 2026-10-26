import { useMemo } from 'react';
import usePlatform from 'Platform/usePlatform';
import { useImportListsData } from 'Settings/ImportLists/ImportLists/useImportLists';

function useQualityProfileInUse(id: number | undefined) {
  const { data: series = [] } = usePlatform();
  const importLists = useImportListsData();

  return useMemo(() => {
    if (!id) {
      return {
        seriesCount: 0,
        importsCount: 0,
      };
    }

    return {
      seriesCount: series.filter((s) => s.qualityProfileId === id).length,
      importListCount: importLists.filter(
        (list) => list.qualityProfileId === id
      ).length,
    };
  }, [id, series, importLists]);
}

export default useQualityProfileInUse;
