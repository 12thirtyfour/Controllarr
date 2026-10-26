import classNames from 'classnames';
import moment from 'moment-timezone';
import React, {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useIsDownloadingGames } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import getFinaleTypeName from 'Game/getFinaleTypeName';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Platform/usePlatform';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { CalendarItem } from 'typings/Calendar';
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import CalendarEvent from './CalendarEvent';
import getCalendarStatusLabel from './getCalendarStatusLabel';
import styles from './CalendarEventGroup.module.css';

interface CalendarEventGroupProps {
  gameIds: number[];
  seriesId: number;
  events: CalendarItem[];
  onEventModalOpenToggle: (isOpen: boolean) => void;
}

function CalendarEventGroup({
  gameIds,
  seriesId,
  events,
  onEventModalOpenToggle,
}: CalendarEventGroupProps) {
  const isDownloading = useIsDownloadingGames(gameIds);
  const series = useSingleSeries(seriesId)!;

  const { timeFormat } = useUiSettingsValues();

  const { showGameInformation, showFinaleIcon, fullColorEvents } =
    useCalendarOptions();

  const [isExpanded, setIsExpanded] = useState(false);
  const expandedContentId = useId();
  const toggleButtonId = useId();
  const restoreToggleFocus = useRef(false);

  const firstGame = events[0];
  const lastGame = events[events.length - 1];
  const airDateUtc = firstGame.airDateUtc;
  const startTime = moment(airDateUtc);
  const endTime = moment(lastGame.airDateUtc).add(series.runtime, 'minutes');
  const seasonNumber = firstGame.seasonNumber;

  const { allDownloaded, anyGrabbed, anyMonitored, allAbsoluteGameNumbers } =
    useMemo(() => {
      let files = 0;
      let grabbed = 0;
      let monitored = 0;
      let absoluteGameNumbers = 0;

      events.forEach((event) => {
        if (event.romFileId) {
          files++;
        }

        if (event.grabbed) {
          grabbed++;
        }

        if (series.monitored && event.monitored) {
          monitored++;
        }

        if (event.absoluteGameNumber) {
          absoluteGameNumbers++;
        }
      });

      return {
        allDownloaded: files === events.length,
        anyGrabbed: grabbed > 0,
        anyMonitored: monitored > 0,
        allAbsoluteGameNumbers: absoluteGameNumbers === events.length,
      };
    }, [series, events]);

  const anyDownloading = isDownloading || anyGrabbed;

  const statusStyle = getStatusStyle(
    allDownloaded,
    anyDownloading,
    startTime,
    endTime,
    anyMonitored
  );
  const isMissingAbsoluteNumber =
    series.seriesType === 'anime' &&
    seasonNumber > 0 &&
    !allAbsoluteGameNumbers;
  const expandLabel = translate('CalendarExpandGameGroup', {
    count: events.length,
    series: series.title,
    season: seasonNumber,
    status: getCalendarStatusLabel(statusStyle),
  });
  const collapseLabel = translate('CalendarCollapseGameGroup', {
    series: series.title,
    season: seasonNumber,
  });

  const handleExpandPress = useCallback(() => {
    restoreToggleFocus.current = true;
    setIsExpanded((state) => !state);
  }, []);

  useEffect(() => {
    if (restoreToggleFocus.current) {
      document.getElementById(toggleButtonId)?.focus();
      restoreToggleFocus.current = false;
    }
  }, [isExpanded, toggleButtonId]);

  if (isExpanded) {
    return (
      <div>
        <div id={expandedContentId}>
          <ul className={styles.expandedEventList}>
            {events.map((event) => {
              return (
                <li key={event.id}>
                  <CalendarEvent
                    gameId={event.id}
                    {...event}
                    onEventModalOpenToggle={onEventModalOpenToggle}
                  />
                </li>
              );
            })}
          </ul>
        </div>

        <Link
          id={toggleButtonId}
          className={styles.collapseContainer}
          aria-label={collapseLabel}
          aria-expanded={true}
          aria-controls={expandedContentId}
          onPress={handleExpandPress}
        >
          <Icon name={icons.COLLAPSE} aria-hidden={true} />
        </Link>
      </div>
    );
  }

  return (
    <div
      className={classNames(
        styles.eventGroup,
        styles[statusStyle],
        fullColorEvents && 'fullColor'
      )}
    >
      <div id={expandedContentId} hidden={true} />

      <div className={styles.info}>
        <div className={styles.platformTitle}>{series.title}</div>

        <div
          className={classNames(
            styles.statusContainer,
            fullColorEvents && 'fullColor'
          )}
        >
          {isMissingAbsoluteNumber ? (
            <Icon
              titleWrapperClassName={styles.statusIcon}
              name={icons.WARNING}
              kind={kinds.WARNING}
              title={translate('GameMissingAbsoluteNumber')}
            />
          ) : null}

          {anyDownloading ? (
            <Icon
              titleWrapperClassName={styles.statusIcon}
              name={icons.DOWNLOADING}
              title={translate('AnGameIsDownloading')}
            />
          ) : null}

          {firstGame.gameNumber === 1 && seasonNumber > 0 ? (
            <Icon
              titleWrapperClassName={styles.statusIcon}
              name={icons.PREMIERE}
              kind={kinds.INFO}
              title={
                seasonNumber === 1
                  ? translate('SeriesPremiere')
                  : translate('SeasonPremiere')
              }
            />
          ) : null}

          {showFinaleIcon && lastGame.finaleType ? (
            <Icon
              titleWrapperClassName={styles.statusIcon}
              name={
                lastGame.finaleType === 'series'
                  ? icons.FINALE_SERIES
                  : icons.FINALE_SEASON
              }
              kind={
                lastGame.finaleType === 'series'
                  ? kinds.DANGER
                  : kinds.WARNING
              }
              title={getFinaleTypeName(lastGame.finaleType)}
            />
          ) : null}
        </div>
      </div>

      <div className={styles.airingInfo}>
        <div className={styles.airTime}>
          {formatTime(airDateUtc, timeFormat)} -{' '}
          {formatTime(endTime.toISOString(), timeFormat, {
            includeMinuteZero: true,
          })}
        </div>

        {showGameInformation ? (
          <div className={styles.gameInfo}>
            {seasonNumber}x{padNumber(firstGame.gameNumber, 2)}-
            {padNumber(lastGame.gameNumber, 2)}
            {series.seriesType === 'anime' &&
            firstGame.absoluteGameNumber &&
            lastGame.absoluteGameNumber ? (
              <span className={styles.absoluteGameNumber}>
                ({firstGame.absoluteGameNumber}-
                {lastGame.absoluteGameNumber})
              </span>
            ) : null}
          </div>
        ) : (
          <Link
            id={toggleButtonId}
            className={styles.expandContainerInline}
            aria-label={expandLabel}
            aria-expanded={false}
            aria-controls={expandedContentId}
            onPress={handleExpandPress}
          >
            <Icon name={icons.EXPAND} aria-hidden={true} />
          </Link>
        )}
      </div>

      {showGameInformation ? (
        <Link
          id={toggleButtonId}
          className={styles.expandContainer}
          aria-label={expandLabel}
          aria-expanded={false}
          aria-controls={expandedContentId}
          onPress={handleExpandPress}
        >
          &nbsp;
          <Icon name={icons.EXPAND} aria-hidden={true} />
          &nbsp;
        </Link>
      ) : null}
    </div>
  );
}

export default CalendarEventGroup;
