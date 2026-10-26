import React from 'react';
import { useQueueDetailsForSeries } from 'Activity/Queue/Details/QueueDetailsProvider';
import Label from 'Components/Label';
import { kinds, sizes } from 'Helpers/Props';

function getGameCountKind(
  monitored: boolean,
  romFileCount: number,
  gameCount: number,
  isDownloading: boolean
) {
  if (isDownloading) {
    return kinds.PURPLE;
  }

  if (romFileCount === gameCount && gameCount > 0) {
    return kinds.SUCCESS;
  }

  if (!monitored) {
    return kinds.WARNING;
  }

  return kinds.DANGER;
}

interface SeriesProgressLabelProps {
  className: string;
  seriesId: number;
  monitored: boolean;
  gameCount: number;
  romFileCount: number;
}

function SeriesProgressLabel({
  className,
  seriesId,
  monitored,
  gameCount,
  romFileCount,
}: SeriesProgressLabelProps) {
  const queueDetails = useQueueDetailsForSeries(seriesId);

  const newDownloads = queueDetails.count - queueDetails.gamesWithFiles;
  const text = newDownloads
    ? `${romFileCount} + ${newDownloads} / ${gameCount}`
    : `${romFileCount} / ${gameCount}`;

  return (
    <Label
      className={className}
      kind={getGameCountKind(
        monitored,
        romFileCount,
        gameCount,
        queueDetails.count > 0
      )}
      size={sizes.LARGE}
    >
      <span>{text}</span>
    </Label>
  );
}

export default SeriesProgressLabel;
