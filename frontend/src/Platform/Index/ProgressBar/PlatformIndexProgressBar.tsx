import React, { useMemo } from 'react';
import { useQueueDetailsForSeries } from 'Activity/Queue/Details/QueueDetailsProvider';
import ProgressBar from 'Components/ProgressBar';
import { sizes } from 'Helpers/Props';
import { PlatformStatus } from 'Platform/Platform';
import getProgressBarKind from 'Utilities/Platform/getProgressBarKind';
import translate from 'Utilities/String/translate';
import styles from './PlatformIndexProgressBar.module.css';

interface PlatformIndexProgressBarProps {
  seriesId: number;
  seasonNumber?: number;
  monitored: boolean;
  status: PlatformStatus;
  gameCount: number;
  romFileCount: number;
  totalGameCount: number;
  width: number;
  detailedProgressBar: boolean;
  isStandalone: boolean;
}

function PlatformIndexProgressBar(props: PlatformIndexProgressBarProps) {
  const {
    seriesId,
    seasonNumber,
    monitored,
    status,
    gameCount,
    romFileCount,
    totalGameCount,
    width,
    detailedProgressBar,
    isStandalone,
  } = props;

  const queueDetails = useQueueDetailsForSeries(seriesId, seasonNumber);

  const containerClassName = useMemo(() => {
    if (isStandalone) {
      return undefined;
    }

    if (detailedProgressBar) {
      return styles.progressDetailed;
    }

    return styles.progress;
  }, [isStandalone, detailedProgressBar]);

  const newDownloads = queueDetails.count - queueDetails.gamesWithFiles;
  const progress = gameCount ? (romFileCount / gameCount) * 100 : 100;
  const text = newDownloads
    ? `${romFileCount} + ${newDownloads} / ${gameCount}`
    : `${romFileCount} / ${gameCount}`;

  return (
    <ProgressBar
      className={
        detailedProgressBar ? styles.progressBarDetailed : styles.progressBar
      }
      containerClassName={containerClassName}
      progress={progress}
      kind={getProgressBarKind(
        status,
        monitored,
        progress,
        queueDetails.count > 0
      )}
      size={detailedProgressBar ? sizes.MEDIUM : sizes.SMALL}
      showText={detailedProgressBar}
      text={text}
      title={translate('SeriesProgressBarText', {
        romFileCount,
        gameCount,
        totalGameCount,
        downloadingCount: queueDetails.count,
      })}
      width={width}
    />
  );
}

export default PlatformIndexProgressBar;
