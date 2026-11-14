import React, { useCallback, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import { useSelect } from 'App/Select/SelectContext';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ProgressBar from 'Components/ProgressBar';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Tooltip from 'Components/Tooltip/Tooltip';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import GameFormats from 'Game/GameFormats';
import GameLanguages from 'Game/GameLanguages';
import { useGamesWithIds } from 'Game/useGame';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import Language from 'Language/Language';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import { useSingleSeries } from 'Platform/usePlatform';
import { QualityModel } from 'Quality/Quality';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { SelectStateInputProps } from 'typings/props';
import Queue, {
  QueueTrackedDownloadState,
  QueueTrackedDownloadStatus,
  StatusMessage,
} from 'typings/Queue';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import GameTitleCellContent from './GameTitleCellContent';
import QueueStatusCell from './QueueStatusCell';
import RemoveQueueItemModal from './RemoveQueueItemModal';
import TimeLeftCell from './TimeLeftCell';
import { useGrabQueueItem, useRemoveQueueItem } from './useQueue';
import styles from './QueueRow.module.css';

interface QueueRowProps {
  id: number;
  seriesId?: number;
  gameIds: number[];
  downloadId: string;
  title: string;
  status: string;
  trackedDownloadStatus?: QueueTrackedDownloadStatus;
  trackedDownloadState?: QueueTrackedDownloadState;
  importingActivity?: string;
  statusMessages?: StatusMessage[];
  errorMessage?: string;
  languages: Language[];
  quality: QualityModel;
  customFormats?: CustomFormat[];
  customFormatScore: number;
  protocol: DownloadProtocol;
  indexer?: string;
  isFullSeason: boolean;
  seasonNumbers: number[];
  outputPath?: string;
  downloadClient?: string;
  downloadClientHasPostImportCategory?: boolean;
  estimatedCompletionTime?: string;
  added?: string;
  timeLeft?: string;
  size: number;
  sizeLeft: number;
  isRemoving?: boolean;
  columns: Column[];
  onQueueRowModalOpenOrClose: (isOpen: boolean) => void;
}

function QueueRow(props: QueueRowProps) {
  const {
    id,
    seriesId,
    gameIds,
    downloadId,
    title,
    status,
    trackedDownloadStatus,
    trackedDownloadState,
    importingActivity,
    statusMessages,
    errorMessage,
    languages,
    customFormats = [],
    customFormatScore,
    protocol,
    indexer,
    outputPath,
    downloadClient,
    downloadClientHasPostImportCategory,
    estimatedCompletionTime,
    added,
    timeLeft,
    size,
    sizeLeft,
    columns,
    onQueueRowModalOpenOrClose,
  } = props;

  const series = useSingleSeries(seriesId);
  const games = useGamesWithIds(gameIds);
  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();
  const { removeQueueItem, isRemoving } = useRemoveQueueItem(id);
  const { grabQueueItem, isGrabbing, grabError } = useGrabQueueItem(id);
  const { toggleSelected, useIsSelected } = useSelect<Queue>();
  const isSelected = useIsSelected(id);

  const [isRemoveQueueItemModalOpen, setIsRemoveQueueItemModalOpen] =
    useState(false);

  const [isInteractiveImportModalOpen, setIsInteractiveImportModalOpen] =
    useState(false);

  const handleGrabPress = useCallback(() => {
    grabQueueItem();
  }, [grabQueueItem]);

  const handleInteractiveImportPress = useCallback(() => {
    onQueueRowModalOpenOrClose(true);
    setIsInteractiveImportModalOpen(true);
  }, [setIsInteractiveImportModalOpen, onQueueRowModalOpenOrClose]);

  const handleInteractiveImportModalClose = useCallback(() => {
    onQueueRowModalOpenOrClose(false);
    setIsInteractiveImportModalOpen(false);
  }, [setIsInteractiveImportModalOpen, onQueueRowModalOpenOrClose]);

  const handleRemoveQueueItemPress = useCallback(() => {
    onQueueRowModalOpenOrClose(true);
    setIsRemoveQueueItemModalOpen(true);
  }, [setIsRemoveQueueItemModalOpen, onQueueRowModalOpenOrClose]);

  const handleRemoveQueueItemModalConfirmed = useCallback(() => {
    onQueueRowModalOpenOrClose(false);
    removeQueueItem();
    setIsRemoveQueueItemModalOpen(false);
  }, [
    setIsRemoveQueueItemModalOpen,
    removeQueueItem,
    onQueueRowModalOpenOrClose,
  ]);

  const handleRemoveQueueItemModalClose = useCallback(() => {
    onQueueRowModalOpenOrClose(false);
    setIsRemoveQueueItemModalOpen(false);
  }, [setIsRemoveQueueItemModalOpen, onQueueRowModalOpenOrClose]);

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  const progress = 100 - (sizeLeft / size) * 100;
  const showInteractiveImport =
    status === 'completed' && trackedDownloadStatus === 'warning';
  const isPending =
    status === 'delay' || status === 'downloadClientUnavailable';

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'status') {
          return (
            <QueueStatusCell
              key={name}
              sourceTitle={title}
              status={status}
              trackedDownloadStatus={trackedDownloadStatus}
              trackedDownloadState={trackedDownloadState}
              importingActivity={importingActivity}
              statusMessages={statusMessages}
              errorMessage={errorMessage}
            />
          );
        }

        if (name === 'series.sortTitle') {
          return (
            <TableRowCell key={name}>
              {series ? (
                <PlatformTitleLink
                  titleSlug={series.titleSlug}
                  title={series.title}
                />
              ) : (
                <span className={styles.unmatchedTitle}>{title}</span>
              )}
            </TableRowCell>
          );
        }

        if (name === 'games.title') {
          return (
            <TableRowCell key={name}>
              <GameTitleCellContent
                games={games}
                series={series}
                fallbackTitle={title}
              />
            </TableRowCell>
          );
        }

        if (name === 'games.airDateUtc') {
          if (games.length === 0) {
            return <TableRowCell key={name}>-</TableRowCell>;
          }

          if (games.length === 1) {
            return <RelativeDateCell key={name} date={games[0].airDateUtc} />;
          }

          return (
            <TableRowCell key={name}>
              <span
                title={`${formatDateTime(
                  games[0].airDateUtc,
                  longDateFormat,
                  timeFormat,
                  {
                    includeRelativeDay: !showRelativeDates,
                  }
                )} - ${formatDateTime(
                  games[games.length - 1].airDateUtc,
                  longDateFormat,
                  timeFormat,
                  {
                    includeRelativeDay: !showRelativeDates,
                  }
                )}`}
              >
                {getRelativeDate({
                  date: games[0].airDateUtc,
                  shortDateFormat,
                  showRelativeDates,
                  timeFormat,
                  timeForToday: true,
                })}
                {' - '}
                {getRelativeDate({
                  date: games[games.length - 1].airDateUtc,
                  shortDateFormat,
                  showRelativeDates,
                  timeFormat,
                  timeForToday: true,
                })}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name}>
              <GameLanguages languages={languages} />
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name} className={styles.customFormatScore}>
              <Tooltip
                anchor={formatCustomFormatScore(
                  customFormatScore,
                  customFormats.length
                )}
                tooltip={<GameFormats formats={customFormats} />}
                position={tooltipPositions.BOTTOM}
              />
            </TableRowCell>
          );
        }

        if (name === 'protocol') {
          return (
            <TableRowCell key={name}>
              <ProtocolLabel protocol={protocol} showSourceName={true} />
            </TableRowCell>
          );
        }

        if (name === 'indexer') {
          return <TableRowCell key={name}>{indexer}</TableRowCell>;
        }

        if (name === 'downloadClient') {
          return <TableRowCell key={name}>{downloadClient}</TableRowCell>;
        }

        if (name === 'title') {
          return <TableRowCell key={name}>{title}</TableRowCell>;
        }

        if (name === 'size') {
          return <TableRowCell key={name}>{formatBytes(size)}</TableRowCell>;
        }

        if (name === 'outputPath') {
          return <TableRowCell key={name}>{outputPath}</TableRowCell>;
        }

        if (name === 'activityStatus') {
          let activityLabel = status || '-';

          if (importingActivity) {
            activityLabel = importingActivity;
          } else if (trackedDownloadState === 'importing') {
            activityLabel = translate('Importing');
          } else if (trackedDownloadState === 'importPending') {
            activityLabel = 'Import Pending';
          } else if (trackedDownloadState === 'importBlocked') {
            activityLabel = 'Import Blocked';
          } else if (trackedDownloadState === 'failedPending') {
            activityLabel = 'Failed Pending';
          } else if (trackedDownloadState === 'failed') {
            activityLabel = translate('Failed');
          } else if (trackedDownloadState === 'imported') {
            activityLabel = translate('Imported');
          } else if (status === 'downloading') {
            activityLabel = translate('Downloading');
          } else if (status === 'queued') {
            activityLabel = translate('Queued');
          } else if (status === 'paused') {
            activityLabel = translate('Paused');
          } else if (status === 'completed') {
            activityLabel = translate('Completed');
          } else if (status === 'delay') {
            activityLabel = 'Delayed';
          } else if (status === 'downloadClientUnavailable') {
            activityLabel = 'Unavailable';
          } else if (status === 'failed') {
            activityLabel = translate('Failed');
          } else if (status === 'warning') {
            activityLabel = 'Warning';
          }

          return (
            <TableRowCell key={name} title={activityLabel}>
              {activityLabel}
            </TableRowCell>
          );
        }

        if (name === 'speed') {
          let speedLabel = '-';

          if (status === 'downloading' && sizeLeft > 0 && timeLeft) {
            const cleanSpan = timeLeft.split('.')[0].includes(':')
              ? timeLeft
              : timeLeft.replace(/^(\d+)\./, '$1:');
            const parts = cleanSpan.split(':').map((p) => parseFloat(p) || 0);
            let totalSeconds = 0;

            if (parts.length === 4) {
              totalSeconds =
                parts[0] * 86400 + parts[1] * 3600 + parts[2] * 60 + parts[3];
            } else if (parts.length === 3) {
              totalSeconds = parts[0] * 3600 + parts[1] * 60 + parts[2];
            }

            if (totalSeconds > 0) {
              speedLabel = `${formatBytes(sizeLeft / totalSeconds)}/s`;
            }
          }

          return (
            <TableRowCell key={name} className={styles.speed}>
              {speedLabel}
            </TableRowCell>
          );
        }

        if (name === 'estimatedCompletionTime') {
          return (
            <TimeLeftCell
              key={name}
              status={status}
              trackedDownloadState={trackedDownloadState}
              importingActivity={importingActivity}
              estimatedCompletionTime={estimatedCompletionTime}
              timeLeft={timeLeft}
              size={size}
              sizeLeft={sizeLeft}
              showRelativeDates={showRelativeDates}
              shortDateFormat={shortDateFormat}
              timeFormat={timeFormat}
            />
          );
        }

        if (name === 'progress') {
          return (
            <TableRowCell key={name} className={styles.progress}>
              {size > 0 && progress >= 0 && (
                <ProgressBar
                  ariaLabel={translate('DownloadProgressForTitle', { title })}
                  progress={progress}
                  title={`${progress.toFixed(1)}%`}
                />
              )}
            </TableRowCell>
          );
        }

        if (name === 'added') {
          return <RelativeDateCell key={name} date={added} />;
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className={styles.actions}>
              {showInteractiveImport ? (
                <IconButton
                  name={icons.INTERACTIVE}
                  aria-label={translate('InteractiveImport')}
                  onPress={handleInteractiveImportPress}
                />
              ) : null}

              {isPending ? (
                <SpinnerIconButton
                  name={icons.DOWNLOAD}
                  kind={grabError ? kinds.DANGER : kinds.DEFAULT}
                  aria-label={translate('Grab')}
                  isSpinning={isGrabbing}
                  onPress={handleGrabPress}
                />
              ) : null}

              <SpinnerIconButton
                title={translate('RemoveFromQueue')}
                name={icons.REMOVE}
                isSpinning={isRemoving}
                onPress={handleRemoveQueueItemPress}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      <InteractiveImportModal
        isOpen={isInteractiveImportModalOpen}
        downloadIds={[downloadId]}
        title={title}
        onModalClose={handleInteractiveImportModalClose}
      />

      <RemoveQueueItemModal
        isOpen={isRemoveQueueItemModalOpen}
        sourceTitle={title}
        canChangeCategory={!!downloadClientHasPostImportCategory}
        canIgnore={!!series}
        isPending={isPending}
        downloadClient={downloadClient}
        onRemovePress={handleRemoveQueueItemModalConfirmed}
        onModalClose={handleRemoveQueueItemModalClose}
      />
    </TableRow>
  );
}

export default QueueRow;
