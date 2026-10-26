import React, {
  PropsWithChildren,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react';
import QueueDetailsProvider from 'Activity/Queue/Details/QueueDetailsProvider';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { OverflowDivider } from 'Components/Page/Toolbar/Overflow';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import Game from 'Game/Game';
import RomFileProvider from 'RomFile/RomFileProvider';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import useMeasure from 'Helpers/Hooks/useMeasure';
import { align, icons } from 'Helpers/Props';
import NoPlatform from 'Platform/NoPlatform';
import { useHasSeries } from 'Platform/usePlatform';
import selectUniqueIds from 'Utilities/Object/selectUniqueIds';
import translate from 'Utilities/String/translate';
import Calendar from './Calendar';
import CalendarFilterModal from './CalendarFilterModal';
import CalendarMissingGameSearchButton from './CalendarMissingGameSearchButton';
import { setCalendarOption, useCalendarOption } from './calendarOptionsStore';
import CalendarLinkModal from './iCal/CalendarLinkModal';
import Legend from './Legend/Legend';
import CalendarOptionsModal from './Options/CalendarOptionsModal';
import useCalendar, {
  FILTERS,
  setCalendarDayCount,
  useCalendarPage,
} from './useCalendar';
import styles from './CalendarPage.module.css';

const MINIMUM_DAY_WIDTH = 120;

function CalendarPage() {
  const executeCommand = useExecuteCommand();

  const selectedFilterKey = useCalendarOption('selectedFilterKey');
  const { data } = useCalendar();

  useCalendarPage();

  const isRssSyncExecuting = useCommandExecuting(CommandNames.RssSync);
  const customFilters = useCustomFiltersList('calendar');
  const hasSeries = useHasSeries();

  const [pageContentRef, { width }] = useMeasure();
  const [isCalendarLinkModalOpen, setIsCalendarLinkModalOpen] = useState(false);
  const [isOptionsModalOpen, setIsOptionsModalOpen] = useState(false);

  const isMeasured = width > 0;
  const PageComponent = hasSeries ? Calendar : NoPlatform;

  const handleGetCalendarLinkPress = useCallback(() => {
    setIsCalendarLinkModalOpen(true);
  }, []);

  const handleGetCalendarLinkModalClose = useCallback(() => {
    setIsCalendarLinkModalOpen(false);
  }, []);

  const handleOptionsPress = useCallback(() => {
    setIsOptionsModalOpen(true);
  }, []);

  const handleOptionsModalClose = useCallback(() => {
    setIsOptionsModalOpen(false);
  }, []);

  const handleRssSyncPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RssSync,
    });
  }, [executeCommand]);

  const handleFilterSelect = useCallback((key: string | number) => {
    setCalendarOption('selectedFilterKey', key);
  }, []);

  const gameIds = useMemo(() => {
    return selectUniqueIds<Game, number>(data, 'id');
  }, [data]);

  const romFileIds = useMemo(() => {
    return selectUniqueIds<Game, number>(data, 'romFileId');
  }, [data]);

  useEffect(() => {
    if (width === 0) {
      return;
    }

    const dayCount = Math.max(
      3,
      Math.min(7, Math.floor(width / MINIMUM_DAY_WIDTH))
    );

    setCalendarDayCount(dayCount);
  }, [width]);

  return (
    <CalendarPageProvider
      gameIds={gameIds}
      romFileIds={romFileIds}
    >
      <PageContent title={translate('Calendar')}>
        <PageToolbar>
          <ToolbarItem
            id="ical-link"
            priority={1}
            groupId="left-a"
            label={translate('ICalLink')}
            iconName={icons.CALENDAR}
            onPress={handleGetCalendarLinkPress}
          />

          <OverflowDivider groupId="left-a">
            <PageToolbarSeparator />
          </OverflowDivider>

          <ToolbarItem
            id="rss-sync"
            priority={1}
            groupId="left-b"
            label={translate('RssSync')}
            iconName={icons.RSS}
            isSpinning={isRssSyncExecuting}
            onPress={handleRssSyncPress}
          />

          <ToolbarItem id="search-missing" priority={1} groupId="left-b">
            <CalendarMissingGameSearchButton />
          </ToolbarItem>

          <PageToolbarSpacer />

          <ToolbarItem
            id="options"
            priority={2}
            groupId="right"
            label={translate('Options')}
            iconName={icons.POSTER}
            onPress={handleOptionsPress}
          />

          <ToolbarItem id="filter" pinned={true}>
            <FilterMenu
              alignMenu={align.RIGHT}
              isDisabled={!hasSeries}
              selectedFilterKey={selectedFilterKey}
              filters={FILTERS}
              customFilters={customFilters}
              filterModalConnectorComponent={CalendarFilterModal}
              onFilterSelect={handleFilterSelect}
            />
          </ToolbarItem>
        </PageToolbar>

        <PageContentBody
          ref={pageContentRef}
          className={styles.calendarPageBody}
          innerClassName={styles.calendarInnerPageBody}
        >
          {isMeasured ? <PageComponent totalItems={0} /> : <div />}
          {hasSeries && <Legend />}
        </PageContentBody>

        <CalendarLinkModal
          isOpen={isCalendarLinkModalOpen}
          onModalClose={handleGetCalendarLinkModalClose}
        />

        <CalendarOptionsModal
          isOpen={isOptionsModalOpen}
          onModalClose={handleOptionsModalClose}
        />
      </PageContent>
    </CalendarPageProvider>
  );
}

export default CalendarPage;

function CalendarPageProvider({
  gameIds,
  romFileIds,
  children,
}: PropsWithChildren<{ gameIds: number[]; romFileIds: number[] }>) {
  return (
    <QueueDetailsProvider gameIds={gameIds}>
      <RomFileProvider romFileIds={romFileIds}>
        {children}
      </RomFileProvider>
    </QueueDetailsProvider>
  );
}
