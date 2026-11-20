import React, { useCallback, useMemo } from 'react';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import { align, kinds } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import roundNumber from 'Utilities/Number/roundNumber';
import translate from 'Utilities/String/translate';
import BarChart, { BarChartItem } from './Charts/BarChart';
import DoughnutChart, { DoughnutChartItem } from './Charts/DoughnutChart';
import StatisticsFilterModal from './StatisticsFilterModal';
import {
  setStatisticsOption,
  useStatisticsOption,
} from './statisticsOptionsStore';
import StatisticsSummary, { SummaryItem } from './StatisticsSummary';
import useChartColors from './useChartColors';
import useStatistics, { FILTERS } from './useStatistics';
import styles from './Statistics.module.css';

function Statistics() {
  const selectedFilterKey = useStatisticsOption('selectedFilterKey');
  const customFilters = useCustomFiltersList('statistics');
  const colors = useChartColors();

  const { data, isLoading, error } = useStatistics();

  const handleFilterSelect = useCallback((key: string | number) => {
    setStatisticsOption('selectedFilterKey', key);
  }, []);

  const summaryItems = useMemo<SummaryItem[]>(() => {
    if (!data) {
      return [];
    }

    const downloadedPercent = data.totalGameCount
      ? roundNumber((data.downloadedGameCount / data.totalGameCount) * 100)
      : 0;

    const wantedGameCount = data.downloadedGameCount + data.missingGameCount;
    const completedPercent = wantedGameCount
      ? roundNumber((data.downloadedGameCount / wantedGameCount) * 100)
      : 0;

    const averageSizePerGame = data.totalGameCount
      ? data.sizeOnDisk / data.totalGameCount
      : 0;

    return [
      {
        label: translate('Platforms'),
        value: (data.platformCount ?? 0).toLocaleString(),
        secondary: `${(
          data.monitoredPlatformCount ?? 0
        ).toLocaleString()} ${translate('Monitored')} · ${(
          data.completedPlatformCount ?? 0
        ).toLocaleString()} ${translate('Completed')}`,
      },
      {
        label: translate('Regions'),
        value: (data.regionCount ?? 0).toLocaleString(),
        secondary: `${(
          data.completedRegionCount ?? 0
        ).toLocaleString()} ${translate('Completed')}`,
      },
      {
        label: translate('TotalGames'),
        value: (data.totalGameCount ?? 0).toLocaleString(),
        secondary: `${(
          data.monitoredGameCount ?? 0
        ).toLocaleString()} ${translate('Monitored')}`,
      },
      {
        label: translate('RomFiles'),
        value: (data.romFileCount ?? 0).toLocaleString(),
        secondary: `${downloadedPercent}% ${translate('Downloaded')}`,
      },
      {
        label: translate('MissingGames'),
        value: (data.missingGameCount ?? 0).toLocaleString(),
        secondary: `${completedPercent}% ${translate('Completed')}`,
      },
      {
        label: translate('SizeOnDisk'),
        value: formatBytes(data.sizeOnDisk),
        secondary: `${formatBytes(averageSizePerGame)} ${translate(
          'AverageSizePerGame'
        )}`,
      },
    ];
  }, [data]);

  const statusItems = useMemo<DoughnutChartItem[]>(() => {
    if (!data) {
      return [];
    }

    return [
      {
        label: translate('Continuing'),
        value: data.continuingPlatformCount,
        color: colors.successColor,
      },
      {
        label: translate('Ended'),
        value: data.endedPlatformCount,
        color: colors.bar,
      },
      {
        label: translate('Upcoming'),
        value: data.upcomingPlatformCount,
        color: colors.warningColor,
      },
      {
        label: translate('Deleted'),
        value: data.deletedPlatformCount,
        color: colors.dangerColor,
      },
    ];
  }, [colors, data]);

  const typeItems = useMemo<DoughnutChartItem[]>(() => {
    if (!data) {
      return [];
    }

    const items: DoughnutChartItem[] = [
      {
        label: translate('Standard'),
        value: data.standardPlatformCount,
        color: colors.bar,
      },
    ];

    if (data.animePlatformCount > 0) {
      items.push({
        label: translate('Anime'),
        value: data.animePlatformCount,
        color: colors.palette[4],
      });
    }

    return items;
  }, [colors, data]);

  const gameItems = useMemo<DoughnutChartItem[]>(() => {
    if (!data) {
      return [];
    }

    const otherCount = Math.max(
      0,
      data.totalGameCount -
        data.downloadedGameCount -
        data.missingGameCount -
        data.unreleasedGameCount
    );

    return [
      {
        label: translate('Downloaded'),
        value: data.downloadedGameCount,
        color: colors.successColor,
      },
      {
        label: translate('Missing'),
        value: data.missingGameCount,
        color: colors.dangerColor,
      },
      {
        label: translate('Unreleased'),
        value: data.unreleasedGameCount,
        color: colors.palette[6],
      },
      {
        label: translate('Unmonitored'),
        value: otherCount,
        color: colors.grayColor,
      },
    ];
  }, [colors, data]);

  const qualityItems = useMemo<BarChartItem[]>(() => {
    if (!data) {
      return [];
    }

    return data.qualityStatistics.map((quality) => {
      return {
        label: quality.quality.name,
        value: quality.romFileCount ?? 0,
        tooltipLines: [
          `${translate('SizeOnDisk')}: ${formatBytes(quality.sizeOnDisk ?? 0)}`,
        ],
      };
    });
  }, [data]);

  const tagItems = useMemo<BarChartItem[]>(() => {
    if (!data) {
      return [];
    }

    return data.tagStatistics.map((tag) => {
      return {
        label: tag.label,
        value: tag.platformCount ?? 0,
        tooltipLines: [
          `${translate('RomFiles')}: ${(
            tag.romFileCount ?? 0
          ).toLocaleString()}`,
          `${translate('SizeOnDisk')}: ${formatBytes(tag.sizeOnDisk ?? 0)}`,
        ],
      };
    });
  }, [data]);

  return (
    <PageContent title={translate('Statistics')}>
      <PageToolbar>
        <PageToolbarSpacer />

        <ToolbarItem id="filter" pinned={true}>
          <FilterMenu
            alignMenu={align.RIGHT}
            selectedFilterKey={selectedFilterKey}
            filters={FILTERS}
            customFilters={customFilters}
            filterModalConnectorComponent={StatisticsFilterModal}
            onFilterSelect={handleFilterSelect}
          />
        </ToolbarItem>
      </PageToolbar>

      <PageContentBody>
        <PageHeading
          scope={translate('Platforms')}
          title={translate('Statistics')}
        />

        {isLoading ? <LoadingIndicator /> : null}

        {!isLoading && error ? (
          <Alert kind={kinds.DANGER}>{translate('StatisticsLoadError')}</Alert>
        ) : null}

        {!isLoading && !error && data ? (
          <div className={styles.chartsContainer}>
            <StatisticsSummary items={summaryItems} />

            <div className={styles.charts}>
              <DoughnutChart
                title={translate('PlatformsByStatus')}
                items={statusItems}
              />

              <DoughnutChart
                title={translate('PlatformType')}
                items={typeItems}
              />

              <DoughnutChart title={translate('Games')} items={gameItems} />
            </div>

            <div className={styles.charts}>
              <BarChart
                title={translate('GameQualities')}
                items={qualityItems}
              />

              {tagItems.length ? (
                <BarChart title={translate('PlatformTags')} items={tagItems} />
              ) : null}
            </div>
          </div>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default Statistics;
