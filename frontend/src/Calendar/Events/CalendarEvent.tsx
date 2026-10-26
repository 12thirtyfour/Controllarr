import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useState } from 'react';
import { useQueueItemForGame } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import GameDetailsModal from 'Game/GameDetailsModal';
import gameEntities from 'Game/gameEntities';
import getFinaleTypeName from 'Game/getFinaleTypeName';
import { useRomFile } from 'RomFile/RomFileProvider';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Platform/usePlatform';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import CalendarEventQueueDetails from './CalendarEventQueueDetails';
import getCalendarStatusLabel from './getCalendarStatusLabel';
import styles from './CalendarEvent.module.css';

interface CalendarEventProps {
  id: number;
  gameId: number;
  seriesId: number;
  romFileId?: number;
  title: string;
  seasonNumber: number;
  gameNumber: number;
  absoluteGameNumber?: number;
  airDateUtc: string;
  monitored: boolean;
  unverifiedSceneNumbering?: boolean;
  finaleType?: string;
  hasFile: boolean;
  grabbed?: boolean;
  onEventModalOpenToggle: (isOpen: boolean) => void;
}

function CalendarEvent(props: CalendarEventProps) {
  const {
    id,
    seriesId,
    romFileId,
    title,
    seasonNumber,
    gameNumber,
    absoluteGameNumber,
    airDateUtc,
    monitored,
    unverifiedSceneNumbering,
    finaleType,
    hasFile,
    grabbed,
    onEventModalOpenToggle,
  } = props;

  const series = useSingleSeries(seriesId);
  const romFile = useRomFile(romFileId);
  const queueItem = useQueueItemForGame(id);

  const { longDateFormat, timeFormat } = useUiSettingsValues();

  const {
    showGameInformation,
    showFinaleIcon,
    showSpecialIcon,
    showCutoffUnmetIcon,
    fullColorEvents,
  } = useCalendarOptions();

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
    onEventModalOpenToggle(true);
  }, [onEventModalOpenToggle]);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
    onEventModalOpenToggle(false);
  }, [onEventModalOpenToggle]);

  if (!series) {
    return null;
  }

  const startTime = moment(airDateUtc);
  const endTime = moment(airDateUtc).add(series.runtime, 'minutes');
  const isDownloading = !!(queueItem || grabbed);
  const isMonitored = series.monitored && monitored;
  const statusStyle = getStatusStyle(
    hasFile,
    isDownloading,
    startTime,
    endTime,
    isMonitored
  );
  const missingAbsoluteNumber =
    series.seriesType === 'anime' && seasonNumber > 0 && !absoluteGameNumber;
  const eventLabel = translate('CalendarOpenGameDetails', {
    series: series.title,
    game: `${seasonNumber}x${padNumber(gameNumber, 2)}`,
    title,
    airDate: formatDateTime(airDateUtc, longDateFormat, timeFormat),
    status: getCalendarStatusLabel(statusStyle),
  });

  return (
    <div
      className={classNames(
        styles.event,
        styles[statusStyle],
        fullColorEvents && 'fullColor'
      )}
    >
      <Link
        className={styles.underlay}
        aria-label={eventLabel}
        onPress={handlePress}
      />

      <div className={styles.overlay}>
        <div className={styles.info}>
          <div className={styles.platformTitle}>{series.title}</div>

          <div
            className={classNames(
              styles.statusContainer,
              fullColorEvents && 'fullColor'
            )}
          >
            {missingAbsoluteNumber ? (
              <Icon
                className={styles.statusIcon}
                name={icons.WARNING}
                kind={kinds.WARNING}
                title={translate('GameMissingAbsoluteNumber')}
              />
            ) : null}

            {unverifiedSceneNumbering && !missingAbsoluteNumber ? (
              <Icon
                className={styles.statusIcon}
                name={icons.WARNING}
                kind={kinds.WARNING}
                title={translate('SceneNumberNotVerified')}
              />
            ) : null}

            {queueItem ? (
              <span className={styles.statusIcon}>
                <CalendarEventQueueDetails {...queueItem} />
              </span>
            ) : null}

            {!queueItem && grabbed ? (
              <Icon
                className={styles.statusIcon}
                name={icons.DOWNLOADING}
                title={translate('GameIsDownloading')}
              />
            ) : null}

            {showCutoffUnmetIcon &&
            !!romFile &&
            romFile.qualityCutoffNotMet ? (
              <Icon
                className={styles.statusIcon}
                name={icons.CUTOFF_NOT_MET}
                kind={kinds.WARNING}
                title={translate('QualityCutoffNotMet')}
              />
            ) : null}

            {gameNumber === 1 && seasonNumber > 0 ? (
              <Icon
                className={styles.statusIcon}
                name={icons.PREMIERE}
                kind={kinds.INFO}
                title={
                  seasonNumber === 1
                    ? translate('SeriesPremiere')
                    : translate('SeasonPremiere')
                }
              />
            ) : null}

            {showFinaleIcon && finaleType ? (
              <Icon
                className={styles.statusIcon}
                name={
                  finaleType === 'series'
                    ? icons.FINALE_SERIES
                    : icons.FINALE_SEASON
                }
                kind={finaleType === 'series' ? kinds.DANGER : kinds.WARNING}
                title={getFinaleTypeName(finaleType)}
              />
            ) : null}

            {showSpecialIcon && (gameNumber === 0 || seasonNumber === 0) ? (
              <Icon
                className={styles.statusIcon}
                name={icons.SPECIAL}
                kind={kinds.PINK}
                title={translate('Special')}
              />
            ) : null}
          </div>
        </div>

        {showGameInformation ? (
          <div className={styles.gameInfo}>
            <div className={styles.gameTitle}>{title}</div>

            <div>
              {seasonNumber}x{padNumber(gameNumber, 2)}
              {series.seriesType === 'anime' && absoluteGameNumber ? (
                <span className={styles.absoluteGameNumber}>
                  ({absoluteGameNumber})
                </span>
              ) : null}
            </div>
          </div>
        ) : null}

        <div className={styles.airTime}>
          {formatTime(airDateUtc, timeFormat)} -{' '}
          {formatTime(endTime.toISOString(), timeFormat, {
            includeMinuteZero: true,
          })}
        </div>
      </div>

      <GameDetailsModal
        isOpen={isDetailsModalOpen}
        gameId={id}
        gameEntity={gameEntities.CALENDAR}
        seriesId={series.id}
        gameTitle={title}
        showOpenSeriesButton={true}
        onModalClose={handleDetailsModalClose}
      />
    </div>
  );
}

export default CalendarEvent;
