import classNames from 'classnames';
import React, { useCallback } from 'react';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import formatSeason from 'Region/formatRegion';
import { Statistics } from 'Platform/Platform';
import { useToggleSeasonMonitored } from 'Platform/usePlatform';
import translate from 'Utilities/String/translate';
import styles from './RegionPassRegion.module.css';

interface RegionPassSeasonProps {
  seriesId: number;
  seasonNumber: number;
  monitored: boolean;
  statistics: Statistics;
}

function RegionPassSeason(props: RegionPassSeasonProps) {
  const {
    seriesId,
    seasonNumber,
    monitored,
    statistics = {
      romFileCount: 0,
      totalGameCount: 0,
      percentOfGames: 0,
    },
  } = props;

  const { romFileCount, totalGameCount, percentOfGames } = statistics;

  const { toggleSeasonMonitored, isTogglingSeasonMonitored } =
    useToggleSeasonMonitored(seriesId);
  const onSeasonMonitoredPress = useCallback(() => {
    toggleSeasonMonitored({ seasonNumber, monitored: !monitored });
  }, [seasonNumber, monitored, toggleSeasonMonitored]);

  return (
    <div className={styles.season}>
      <div className={styles.info}>
        <MonitorToggleButton
          monitored={monitored}
          isSaving={isTogglingSeasonMonitored}
          onPress={onSeasonMonitoredPress}
        />

        <span>{formatSeason(seasonNumber, true)}</span>
      </div>

      <div
        className={classNames(
          styles.games,
          percentOfGames === 100 && styles.allGames
        )}
        title={translate('RegionPassGamesDownloaded', {
          romFileCount,
          totalGameCount,
        })}
      >
        {totalGameCount === 0
          ? '0/0'
          : `${romFileCount}/${totalGameCount}`}
      </div>
    </div>
  );
}

export default RegionPassSeason;
