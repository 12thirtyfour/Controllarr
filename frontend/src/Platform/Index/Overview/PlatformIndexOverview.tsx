import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import useMeasure from 'Helpers/Hooks/useMeasure';
import { icons } from 'Helpers/Props';
import DeletePlatformModal from 'Platform/Delete/DeletePlatformModal';
import EditPlatformModal from 'Platform/Edit/EditPlatformModal';
import PlatformIndexProgressBar from 'Platform/Index/ProgressBar/PlatformIndexProgressBar';
import PlatformIndexPosterSelect from 'Platform/Index/Select/PlatformIndexPosterSelect';
import { PlatformStatus, Statistics } from 'Platform/Platform';
import { usePlatformOverviewOptions } from 'Platform/platformOptionsStore';
import { useToggleSeriesMonitored } from 'Platform/usePlatform';
import PlatformPoster from 'Platform/PlatformPoster';
import translate from 'Utilities/String/translate';
import usePlatformIndexItem from '../usePlatformIndexItem';
import { ROW_BORDER, ROW_VERTICAL_PADDING } from './overviewLayout';
import PlatformIndexOverviewInfo from './PlatformIndexOverviewInfo';
import styles from './PlatformIndexOverview.module.css';

// Mirrors .synopsis font-size/line-height in PlatformIndexOverview.css.
const SYNOPSIS_FONT_SIZE = 13.5;
const SYNOPSIS_LINE_HEIGHT = 1.6;

const TITLE_HEIGHT = 38;
const CHIP_STRIP_HEIGHT = 28;

interface PlatformIndexOverviewProps {
  seriesId: number;
  sortKey: string;
  posterWidth: number;
  posterHeight: number;
  rowHeight: number;
  isSelectMode: boolean;
}

function getStatusBadge(status: PlatformStatus) {
  switch (status) {
    case 'deleted':
      return { label: translate('Deleted'), statusClass: styles.deleted };
    case 'continuing':
      return { label: translate('Continuing'), statusClass: styles.continuing };
    case 'upcoming':
      return { label: translate('Upcoming'), statusClass: styles.upcoming };
    case 'ended':
      return { label: translate('Ended'), statusClass: styles.ended };
    default:
      return null;
  }
}

function PlatformIndexOverview(props: PlatformIndexOverviewProps) {
  const {
    seriesId,
    sortKey,
    posterWidth,
    posterHeight,
    rowHeight,
    isSelectMode,
  } = props;

  const { series, qualityProfile, isRefreshingSeries, isSearchingSeries } =
    usePlatformIndexItem(seriesId);

  const overviewOptions = usePlatformOverviewOptions();

  const executeCommand = useExecuteCommand();
  const { toggleSeriesMonitored, isTogglingSeriesMonitored } =
    useToggleSeriesMonitored(seriesId);

  const onMonitorPress = useCallback(() => {
    toggleSeriesMonitored({ monitored: !series?.monitored });
  }, [series?.monitored, toggleSeriesMonitored]);
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

  const contentHeight = useMemo(() => {
    return rowHeight - (ROW_VERTICAL_PADDING * 2 + ROW_BORDER);
  }, [rowHeight]);

  const [chipStripRef, chipStripBounds] = useMeasure();
  const chipStripHeight = chipStripBounds.height || CHIP_STRIP_HEIGHT;

  const synopsisLines = Math.max(
    1,
    Math.floor(
      (contentHeight - TITLE_HEIGHT - chipStripHeight) /
        (SYNOPSIS_FONT_SIZE * SYNOPSIS_LINE_HEIGHT)
    )
  );

  if (!series) {
    return null;
  }

  const {
    title,
    monitored,
    status,
    path,
    titleSlug,
    nextAiring,
    previousAiring,
    added,
    overview,
    statistics = {} as Statistics,
    images,
    tags,
    network,
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

  const statusBadge = getStatusBadge(status);

  return (
    <div>
      <div className={styles.content}>
        <div className={styles.poster}>
          <div className={styles.posterContainer}>
            {isSelectMode ? (
              <PlatformIndexPosterSelect
                seriesId={seriesId}
                titleSlug={titleSlug}
              />
            ) : null}

            {statusBadge ? (
              <div
                className={classNames(
                  styles.statusPill,
                  statusBadge.statusClass
                )}
              >
                <span className={styles.statusLabel}>{statusBadge.label}</span>
              </div>
            ) : null}

            <Link className={styles.link} style={elementStyle} to={link}>
              <PlatformPoster
                className={styles.posterImage}
                style={elementStyle}
                images={images}
                size={250}
                lazy={false}
                overflow={true}
                title={title}
              />
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
            detailedProgressBar={overviewOptions.detailedProgressBar}
            isStandalone={false}
          />
        </div>

        <div className={styles.info} style={{ maxHeight: contentHeight }}>
          <div className={styles.titleRow}>
            <Link className={styles.title} to={link}>
              {title}
            </Link>

            <div className={styles.actions}>
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
                onPress={onRefreshPress}
              />

              {overviewOptions.showSearchAction ? (
                <SpinnerIconButton
                  className={styles.action}
                  name={icons.SEARCH}
                  title={translate('SearchForMonitoredGames')}
                  isSpinning={isSearchingSeries}
                  onPress={onSearchPress}
                />
              ) : null}

              <IconButton
                className={styles.action}
                name={icons.EDIT}
                title={translate('EditPlatform')}
                aria-label={translate('EditPlatform')}
                onPress={onEditPlatformPress}
              />
            </div>
          </div>

          <Link
            className={styles.synopsis}
            to={link}
            style={{ WebkitLineClamp: synopsisLines }}
          >
            {overview}
          </Link>

          <PlatformIndexOverviewInfo
            ref={chipStripRef}
            monitored={monitored}
            network={network}
            nextAiring={nextAiring}
            previousAiring={previousAiring}
            added={added}
            seasonCount={seasonCount}
            qualityProfile={qualityProfile}
            sizeOnDisk={sizeOnDisk}
            path={path}
            sortKey={sortKey}
            showNetwork={overviewOptions.showNetwork}
            showMonitored={overviewOptions.showMonitored}
            showQualityProfile={overviewOptions.showQualityProfile}
            showPreviousAiring={overviewOptions.showPreviousAiring}
            showAdded={overviewOptions.showAdded}
            showSeasonCount={overviewOptions.showSeasonCount}
            showPath={overviewOptions.showPath}
            showSizeOnDisk={overviewOptions.showSizeOnDisk}
            showTags={overviewOptions.showTags}
            onMonitorPress={onMonitorPress}
            tags={tags}
          />
        </div>
      </div>

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

export default PlatformIndexOverview;
