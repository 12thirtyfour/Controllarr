import classNames from 'classnames';
import React, { forwardRef, ReactNode, useCallback } from 'react';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { useTagList } from 'Tags/useTags';
import sortByProp from 'Utilities/Array/sortByProp';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './PlatformIndexOverviewInfo.module.css';

interface PlatformIndexOverviewInfoProps {
  showMonitored: boolean;
  showAdded: boolean;
  showSeasonCount: boolean;
  showPath: boolean;
  showSizeOnDisk: boolean;
  showTags: boolean;
  monitored: boolean;
  added?: string;
  seasonCount: number;
  path: string;
  sizeOnDisk?: number;
  tags: number[];
  sortKey: string;
  onMonitorPress?: () => void;
}

const PlatformIndexOverviewInfo = forwardRef<
  HTMLDivElement,
  PlatformIndexOverviewInfoProps
>((props, ref) => {
  const {
    showMonitored,
    showAdded,
    showSeasonCount,
    showPath,
    showSizeOnDisk,
    showTags,
    monitored,
    added,
    seasonCount,
    path,
    sizeOnDisk = 0,
    tags,
    sortKey,
    onMonitorPress,
  } = props;

  const uiSettings = useUiSettingsValues();
  const { shortDateFormat, showRelativeDates, longDateFormat, timeFormat } =
    uiSettings;
  const tagList = useTagList();

  const handleMonitorKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLSpanElement>) => {
      if (e.key === 'Enter' || e.key === ' ') {
        onMonitorPress?.();
      }
    },
    [onMonitorPress]
  );

  const chips: ReactNode[] = [];

  if (showMonitored || sortKey === 'monitored') {
    chips.push(
      onMonitorPress ? (
        <span
          key="monitored"
          className={classNames(styles.chip, styles.interactiveChip)}
          role="button"
          tabIndex={0}
          title={
            monitored
              ? translate('ToggleMonitoredToUnmonitored')
              : translate('ToggleUnmonitoredToMonitored')
          }
          onClick={onMonitorPress}
          onKeyDown={handleMonitorKeyDown}
        >
          {monitored ? translate('Monitored') : translate('Unmonitored')}
        </span>
      ) : (
        <span key="monitored" className={styles.chip}>
          {monitored ? translate('Monitored') : translate('Unmonitored')}
        </span>
      )
    );
  }

  if (added && (showAdded || sortKey === 'added')) {
    chips.push(
      <span
        key="added"
        className={styles.chip}
        title={formatDateTime(added, longDateFormat, timeFormat)}
      >
        <span className={styles.dateLabel}>{translate('Added')}</span>
        {getRelativeDate({
          date: added,
          shortDateFormat,
          showRelativeDates,
          timeFormat,
          timeForToday: true,
        })}
      </span>
    );
  }

  if (showSeasonCount || sortKey === 'seasonCount') {
    let seasons = translate('OneSeason');

    if (seasonCount === 0) {
      seasons = translate('NoSeasons');
    } else if (seasonCount > 1) {
      seasons = translate('CountSeasons', { count: seasonCount });
    }

    chips.push(
      <span key="seasonCount" className={styles.chip}>
        {seasons}
      </span>
    );
  }

  if (sizeOnDisk > 0 && (showSizeOnDisk || sortKey === 'sizeOnDisk')) {
    chips.push(
      <span key="sizeOnDisk" className={styles.chip}>
        {formatBytes(sizeOnDisk)}
      </span>
    );
  }

  if (showTags && tags.length > 0) {
    tags
      .map((id) => tagList.find((tag) => tag.id === id))
      .filter((tag): tag is NonNullable<typeof tag> => !!tag)
      .sort(sortByProp('label'))
      .forEach((tag) => {
        chips.push(
          <span
            key={`tag-${tag.id}`}
            className={classNames(styles.chip, styles.chipTag)}
          >
            {tag.label}
          </span>
        );
      });
  }

  if (path && (showPath || sortKey === 'path')) {
    chips.push(
      <span key="path" className={styles.chipPath}>
        {path}
      </span>
    );
  }

  if (chips.length === 0) {
    return null;
  }

  return (
    <div ref={ref} className={styles.chipStrip}>
      {chips}
    </div>
  );
});

export default PlatformIndexOverviewInfo;
