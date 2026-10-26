import { maxBy } from 'lodash';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting } from 'Commands/useCommands';
import { Season } from 'Platform/Platform';
import { useSingleSeries } from 'Platform/usePlatform';
import usePlatformQualityProfile from 'Platform/usePlatformQualityProfile';

export function usePlatformIndexItem(seriesId: number) {
  const series = useSingleSeries(seriesId);
  const qualityProfile = usePlatformQualityProfile(series);

  const isRefreshingSeries = useCommandExecuting(CommandNames.RefreshSeries, {
    seriesIds: [seriesId],
  });

  const isSearchingSeries = useCommandExecuting(CommandNames.SeriesSearch, {
    seriesId,
  });

  const latestSeason: Season | undefined = maxBy(
    series?.seasons || [],
    (season) => season.seasonNumber
  );

  return {
    series,
    qualityProfile,
    latestSeason,
    isRefreshingSeries,
    isSearchingSeries,
  };
}

export default usePlatformIndexItem;
