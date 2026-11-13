import React from 'react';
import Icon from 'Components/Icon';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import { QueueTrackedDownloadState } from 'typings/Queue';
import formatTime from 'Utilities/Date/formatTime';
import formatTimeSpan from 'Utilities/Date/formatTimeSpan';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './TimeLeftCell.module.css';

interface TimeLeftCellProps {
  estimatedCompletionTime?: string;
  timeLeft?: string;
  status: string;
  trackedDownloadState?: QueueTrackedDownloadState;
  importingActivity?: string;
  size: number;
  sizeLeft: number;
  showRelativeDates: boolean;
  shortDateFormat: string;
  timeFormat: string;
}

function TimeLeftCell(props: TimeLeftCellProps) {
  const {
    estimatedCompletionTime,
    timeLeft,
    status,
    trackedDownloadState,
    importingActivity,
    size,
    sizeLeft,
    showRelativeDates,
    shortDateFormat,
    timeFormat,
  } = props;

  if (status === 'delay') {
    const date = getRelativeDate({
      date: estimatedCompletionTime,
      shortDateFormat,
      showRelativeDates,
    });
    const time = formatTime(estimatedCompletionTime, timeFormat, {
      includeMinuteZero: true,
    });

    return (
      <TableRowCell className={styles.timeLeft}>
        <Tooltip
          anchor={<Icon name={icons.INFO} />}
          tooltip={translate('DelayingDownloadUntil', { date, time })}
          kind={kinds.INVERSE}
          position={tooltipPositions.TOP}
        />
      </TableRowCell>
    );
  }

  if (status === 'downloadClientUnavailable') {
    const date = getRelativeDate({
      date: estimatedCompletionTime,
      shortDateFormat,
      showRelativeDates,
    });
    const time = formatTime(estimatedCompletionTime, timeFormat, {
      includeMinuteZero: true,
    });

    return (
      <TableRowCell className={styles.timeLeft}>
        <Tooltip
          anchor={<Icon name={icons.INFO} />}
          tooltip={translate('RetryingDownloadOn', { date, time })}
          kind={kinds.INVERSE}
          position={tooltipPositions.TOP}
        />
      </TableRowCell>
    );
  }

  if (trackedDownloadState === 'importing' || importingActivity) {
    const label = importingActivity || translate('Importing');
    return (
      <TableRowCell className={styles.timeLeft} title={label}>
        {label}
      </TableRowCell>
    );
  }

  if (!timeLeft || status === 'completed' || status === 'failed') {
    return <TableRowCell className={styles.timeLeft}>-</TableRowCell>;
  }

  const totalSize = formatBytes(size);
  const remainingSize = formatBytes(sizeLeft);

  return (
    <TableRowCell
      className={styles.timeLeft}
      title={`${remainingSize} / ${totalSize}`}
    >
      {formatTimeSpan(timeLeft)}
    </TableRowCell>
  );
}

export default TimeLeftCell;
