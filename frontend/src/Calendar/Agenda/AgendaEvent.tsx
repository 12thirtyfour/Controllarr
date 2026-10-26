import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useState } from 'react';
import { useQueueItemForGame } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import CalendarEventQueueDetails from 'Calendar/Events/CalendarEventQueueDetails';
import getCalendarStatusLabel from 'Calendar/Events/getCalendarStatusLabel';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import GameDetailsModal from 'Game/GameDetailsModal';
import gameEntities from 'Game/gameEntities';
import getFinaleTypeName from 'Game/getFinaleTypeName';
import { useRomFile } from 'RomFile/RomFileProvider';
import { icons, kinds } from 'Helpers/Props';
import PlatformPoster from 'Platform/PlatformPoster';
import { useSingleSeries } from 'Platform/usePlatform';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import styles from './AgendaEvent.module.css';

interface AgendaEventProps {
  id: number;
  seriesId: number;
  romFileId: number;
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
}

function AgendaEvent(props: AgendaEventProps) {
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
  } = props;

  const series = useSingleSeries(seriesId)!;
  const romFile = useRomFile(romFileId);
  const queueItem = useQueueItemForGame(id);
  const { longDateFormat, timeFormat } = useUiSettingsValues();

  const {
    showCoverArt,
    showGameInformation,
    showFinaleIcon,
    showSpecialIcon,
    showCutoffUnmetIcon,
  } = useCalendarOptions();

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const startTime = moment(airDateUtc);
  const endTime = moment(airDateUtc).add(series.runtime, 'minutes');
  const downloading = !!(queueItem || grabbed);
  const isMonitored = series.monitored && monitored;
  const statusStyle = getStatusStyle(
    hasFile,
    downloading,
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

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  return (
    <div className={styles.event}>
      <Link
        className={styles.underlay}
        aria-label={eventLabel}
        onPress={handlePress}
      />

      {showCoverArt ? (
        <PlatformPoster
          className={styles.cover}
          images={series.images}
          title={series.title}
          size={250}
          lazy={false}
        />
      ) : null}

      <div className={styles.overlay}>
        <div className={classNames(styles.eventWrapper, styles[statusStyle])}>
          <div className={styles.time}>
            {formatTime(airDateUtc, timeFormat)} -{' '}
            {formatTime(endTime.toISOString(), timeFormat, {
              includeMinuteZero: true,
            })}
          </div>

          <div className={styles.platformTitle}>{series.title}</div>

          {showGameInformation ? (
            <div className={styles.seasonGameNumber}>
              {seasonNumber}x{padNumber(gameNumber, 2)}
              {series.seriesType === 'anime' && absoluteGameNumber && (
                <span className={styles.absoluteGameNumber}>
                  ({absoluteGameNumber})
                </span>
              )}
              <div className={styles.gameSeparator}> - </div>
            </div>
          ) : null}

          <div className={styles.gameTitle}>
            {showGameInformation ? title : null}
          </div>

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
          romFile &&
          romFile.qualityCutoffNotMet ? (
            <Icon
              className={styles.statusIcon}
              name={icons.CUTOFF_NOT_MET}
              kind={kinds.WARNING}
              title={translate('QualityCutoffNotMet')}
            />
          ) : null}

          {gameNumber === 1 && seasonNumber > 0 && (
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
          )}

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

export default AgendaEvent;
