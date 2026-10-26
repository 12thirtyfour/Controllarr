import React from 'react';
import { useQueueItemForGame } from 'Activity/Queue/Details/QueueDetailsProvider';
import QueueDetails from 'Activity/Queue/QueueDetails';
import Icon from 'Components/Icon';
import ProgressBar from 'Components/ProgressBar';
import StatusIndicator from 'Components/StatusIndicator';
import useGame, { GameEntity } from 'Game/useGame';
import { useRomFile } from 'RomFile/RomFileProvider';
import { icons, kinds, sizes } from 'Helpers/Props';
import isBefore from 'Utilities/Date/isBefore';
import translate from 'Utilities/String/translate';
import GameQuality from './GameQuality';
import styles from './GameStatus.module.css';

interface GameStatusProps {
  gameId: number;
  gameEntity?: GameEntity;
  romFileId: number | undefined;
}

function GameStatus({
  gameId,
  gameEntity = 'games',
  romFileId,
}: GameStatusProps) {
  const game = useGame(gameId, gameEntity);
  const queueItem = useQueueItemForGame(gameId);
  const romFile = useRomFile(romFileId);

  const { airDateUtc, grabbed, monitored } = game || {};
  const hasRomFile = !!romFile;
  const isQueued = !!queueItem;
  const hasAired = isBefore(airDateUtc);

  if (!game) {
    return null;
  }

  if (isQueued) {
    const { sizeLeft, size } = queueItem;

    const progress = size ? 100 - (sizeLeft / size) * 100 : 0;

    return (
      <StatusIndicator
        className={styles.center}
        label={translate('GameIsDownloading')}
      >
        <QueueDetails
          {...queueItem}
          progressBar={
            <ProgressBar
              progress={progress}
              kind={kinds.PURPLE}
              size={sizes.MEDIUM}
            />
          }
        />
      </StatusIndicator>
    );
  }

  if (grabbed) {
    const label = translate('GameIsDownloading');

    return (
      <StatusIndicator className={styles.center} label={label} title={label}>
        <Icon name={icons.DOWNLOADING} />
      </StatusIndicator>
    );
  }

  if (hasRomFile) {
    const quality = romFile.quality;
    const isCutoffNotMet = romFile.qualityCutoffNotMet;
    const label = translate('GameDownloaded');

    return (
      <StatusIndicator className={styles.center} label={label}>
        <GameQuality
          quality={quality}
          size={romFile.size}
          isCutoffNotMet={isCutoffNotMet}
          title={label}
        />
      </StatusIndicator>
    );
  }

  if (!airDateUtc) {
    const label = translate('Tba');

    return (
      <StatusIndicator className={styles.center} label={label} title={label}>
        <Icon name={icons.TBA} />
      </StatusIndicator>
    );
  }

  if (!monitored) {
    const label = translate('GameIsNotMonitored');

    return (
      <StatusIndicator className={styles.center} label={label} title={label}>
        <Icon name={icons.MONITORED} kind={kinds.DISABLED} size={14} />
      </StatusIndicator>
    );
  }

  if (hasAired) {
    const label = translate('GameMissingFromDisk');

    return (
      <StatusIndicator className={styles.center} label={label} title={label}>
        <Icon name={icons.MISSING} />
      </StatusIndicator>
    );
  }

  const label = translate('GameHasNotAired');

  return (
    <StatusIndicator className={styles.center} label={label} title={label}>
      <Icon name={icons.NOT_AIRED} />
    </StatusIndicator>
  );
}

export default GameStatus;
