import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import PlatformTagList from 'Components/PlatformTagList';
import { icons } from 'Helpers/Props';
import DeletePlatformModal from 'Platform/Delete/DeletePlatformModal';
import EditPlatformModal from 'Platform/Edit/EditPlatformModal';
import PlatformIndexProgressBar from 'Platform/Index/ProgressBar/PlatformIndexProgressBar';
import PlatformIndexPosterSelect from 'Platform/Index/Select/PlatformIndexPosterSelect';
import { PlatformStatus, Statistics } from 'Platform/Platform';
import { useToggleSeriesMonitored } from 'Platform/usePlatform';
import {
  ShowStatusMode,
  usePlatformPosterOptions,
} from 'Platform/platformOptionsStore';
import PlatformPoster from 'Platform/PlatformPoster';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import translate from 'Utilities/String/translate';
import usePlatformIndexItem from '../usePlatformIndexItem';
import PlatformIndexPosterInfo from './PlatformIndexPosterInfo';
import styles from './PlatformIndexPoster.module.css';

interface PlatformIndexPosterProps {
  seriesId: number;
  sortKey: string;
  isSelectMode: boolean;
  posterWidth: number;
  posterHeight: number;
}

function getStatusBadge(status: PlatformStatus, showStatuses: ShowStatusMode[]) {
  if (status === 'deleted' && showStatuses.includes('deleted')) {
    return { label: translate('Deleted'), statusClass: styles.deleted };
  }

  if (status === 'continuing' && showStatuses.includes('continuing')) {
    return { label: translate('Continuing'), statusClass: styles.continuing };
  }

  if (status === 'upcoming' && showStatuses.includes('upcoming')) {
    return { label: translate('Upcoming'), statusClass: styles.upcoming };
  }

  if (status === 'ended' && showStatuses.includes('ended')) {
    return { label: translate('Ended'), statusClass: styles.ended };
  }

  return null;
}

function PlatformIndexPoster(props: PlatformIndexPosterProps) {
  const { seriesId, sortKey, isSelectMode, posterWidth, posterHeight } = props;

  const { series, qualityProfile, isRefreshingSeries, isSearchingSeries } =
    usePlatformIndexItem(seriesId);

  const {
    detailedProgressBar,
    showTitle,
    showMonitored,
    showQualityProfile,
    showTags,
    showSearchAction,
    showStatuses,
  } = usePlatformPosterOptions();

  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();

  const executeCommand = useExecuteCommand();
  const { toggleSeriesMonitored, isTogglingSeriesMonitored } =
    useToggleSeriesMonitored(seriesId);

  const onMonitorPress = useCallback(() => {
    toggleSeriesMonitored({ monitored: !series?.monitored });
  }, [series?.monitored, toggleSeriesMonitored]);
  const [hasPosterError, setHasPosterError] = useState(false);
  const [isEditPlatformModalOpen, setIsEditPlatformModalOpen] = useState(false);
  const [isDeletePlatformModalOpen, setIsDeletePlatformModalOpen] = useState(false);

  const onRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshSeries,
      seriesIds: [seriesId],
    });
  }, [seriesId, executeCommand]);

  const onSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeriesSearch,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const onPosterLoadError = useCallback(() => {
    setHasPosterError(true);
  }, [setHasPosterError]);

  const onPosterLoad = useCallback(() => {
    setHasPosterError(false);
  }, [setHasPosterError]);

  const onEditPlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(true);
  }, [setIsEditPlatformModalOpen]);

  const onEditPlatformModalClose = useCallback(() => {
    setIsEditPlatformModalOpen(false);
  }, [setIsEditPlatformModalOpen]);

  const onDeletePlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(false);
    setIsDeletePlatformModalOpen(true);
  }, [setIsDeletePlatformModalOpen]);

  const onDeletePlatformModalClose = useCallback(() => {
    setIsDeletePlatformModalOpen(false);
  }, [setIsDeletePlatformModalOpen]);

  if (!series) {
    return null;
  }

  const {
    title,
    monitored,
    status,
    path,
    titleSlug,
    originalCountry,
    originalLanguage,
    network,
    nextAiring,
    previousAiring,
    added,
    statistics = {} as Statistics,
    images,
    ratings,
    tags,
  } = series;

  const {
    seasonCount = 0,
    gameCount = 0,
    romFileCount = 0,
    totalGameCount = 0,
    sizeOnDisk = 0,
  } = statistics;

  const link = `/platforms/${titleSlug}`;

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
  };

  const statusBadge = getStatusBadge(status, showStatuses);

  const monitoredLabel = monitored
    ? translate('Monitored')
    : translate('Unmonitored');

  return (
    <div className={styles.content}>
      <div className={styles.posterContainer} title={title}>
        {isSelectMode ? (
          <PlatformIndexPosterSelect seriesId={seriesId} titleSlug={titleSlug} />
        ) : null}

        <div className={styles.controls}>
          <MonitorToggleButton
            className={styles.action}
            monitored={monitored}
            isSaving={isTogglingSeriesMonitored}
            onPress={onMonitorPress}
          />
          <SpinnerIconButton
            className={styles.action}
            name={icons.REFRESH}
            title={translate('RefreshSeries')}
            isSpinning={isRefreshingSeries}
            tabIndex={-1}
            onPress={onRefreshPress}
          />

          {showSearchAction ? (
            <SpinnerIconButton
              className={styles.action}
              name={icons.SEARCH}
              title={translate('SearchForMonitoredGames')}
              isSpinning={isSearchingSeries}
              tabIndex={-1}
              onPress={onSearchPress}
            />
          ) : null}

          <IconButton
            className={styles.action}
            name={icons.EDIT}
            title={translate('EditPlatform')}
            aria-label={translate('EditPlatform')}
            tabIndex={-1}
            onPress={onEditPlatformPress}
          />
        </div>

        {statusBadge ? (
          <div
            className={classNames(styles.status, statusBadge.statusClass)}
            title={statusBadge.label}
          >
            <span className={styles.statusLabel}>{statusBadge.label}</span>
          </div>
        ) : null}

        <Link className={styles.link} style={elementStyle} to={link}>
          <PlatformPoster
            style={elementStyle}
            images={images}
            size={250}
            lazy={false}
            overflow={true}
            title={title}
            onError={onPosterLoadError}
            onLoad={onPosterLoad}
          />

          {hasPosterError ? (
            <div className={styles.overlayTitle}>{title}</div>
          ) : null}
        </Link>
      </div>

      <PlatformIndexProgressBar
        seriesId={seriesId}
        monitored={monitored}
        status={status}
        gameCount={gameCount}
        romFileCount={romFileCount}
        totalGameCount={totalGameCount}
        width={posterWidth}
        detailedProgressBar={detailedProgressBar}
        isStandalone={false}
      />

      {showTitle ? (
        <div className={styles.title} title={title}>
          {title}
        </div>
      ) : null}

      {showMonitored || (showQualityProfile && !!qualityProfile?.name) ? (
        <div className={styles.meta}>
          {showMonitored ? (
            <Link
              className={styles.monitoredLink}
              title={
                monitored
                  ? translate('ToggleMonitoredToUnmonitored')
                  : translate('ToggleUnmonitoredToMonitored')
              }
              onPress={onMonitorPress}
            >
              {monitoredLabel}
            </Link>
          ) : null}

          {showMonitored && showQualityProfile && !!qualityProfile?.name ? (
            <span className={styles.metaSep}> · </span>
          ) : null}
          {showQualityProfile && !!qualityProfile?.name
            ? qualityProfile.name
            : null}
        </div>
      ) : null}

      {nextAiring ? (
        <div
          className={styles.nextAiring}
          title={`${translate('NextAiring')}: ${formatDateTime(
            nextAiring,
            longDateFormat,
            timeFormat
          )}`}
        >
          {getRelativeDate({
            date: nextAiring,
            shortDateFormat,
            showRelativeDates,
            timeFormat,
            timeForToday: true,
          })}
        </div>
      ) : null}

      {showTags && tags.length ? (
        <div className={styles.tags}>
          <div className={styles.tagsList}>
            <PlatformTagList tags={tags} />
          </div>
        </div>
      ) : null}

      <PlatformIndexPosterInfo
        originalCountry={originalCountry}
        originalLanguage={originalLanguage}
        network={network}
        previousAiring={previousAiring}
        added={added}
        seasonCount={seasonCount}
        sizeOnDisk={sizeOnDisk}
        path={path}
        qualityProfile={qualityProfile}
        showQualityProfile={showQualityProfile}
        showRelativeDates={showRelativeDates}
        sortKey={sortKey}
        shortDateFormat={shortDateFormat}
        longDateFormat={longDateFormat}
        timeFormat={timeFormat}
        tags={tags}
        showTags={showTags}
        ratings={ratings}
      />

      <EditPlatformModal
        isOpen={isEditPlatformModalOpen}
        seriesId={seriesId}
        onModalClose={onEditPlatformModalClose}
        onDeletePlatformPress={onDeletePlatformPress}
      />

      <DeletePlatformModal
        isOpen={isDeletePlatformModalOpen}
        seriesId={seriesId}
        onModalClose={onDeletePlatformModalClose}
      />
    </div>
  );
}

export default PlatformIndexPoster;
