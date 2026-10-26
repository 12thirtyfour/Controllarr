import classNames from 'classnames';
import React, { useCallback, useMemo, useRef, useState } from 'react';
import QueueDetailsProvider from 'Activity/Queue/Details/QueueDetailsProvider';
import { useAppDimension } from 'App/appStore';
import { SelectProvider, useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageJumpBar, { PageJumpBarItems } from 'Components/Page/PageJumpBar';
import { OverflowDivider } from 'Components/Page/Toolbar/Overflow';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import { PageToolbarButtonProps } from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import TableOptionsModal from 'Components/Table/TableOptions/TableOptionsModal';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import { icons, kinds } from 'Helpers/Props';
import { DESCENDING } from 'Helpers/Props/sortDirections';
import NoPlatform from 'Platform/NoPlatform';
import Series from 'Platform/Platform';
import {
  setSeriesOption,
  setSeriesSort,
  setSeriesTableOptions,
  usePlatformOptions,
} from 'Platform/platformOptionsStore';
import { FILTERS, usePlatformIndex } from 'Platform/usePlatform';
import { TableOptionsChangePayload } from 'typings/Table';
import translate from 'Utilities/String/translate';
import PlatformIndexFilterMenu from './Menus/PlatformIndexFilterMenu';
import PlatformIndexSortMenu from './Menus/PlatformIndexSortMenu';
import PlatformIndexViewMenu from './Menus/PlatformIndexViewMenu';
import PlatformIndexOverviewOptionsModal from './Overview/Options/PlatformIndexOverviewOptionsModal';
import PlatformIndexOverviews from './Overview/PlatformIndexOverviews';
import PlatformIndexPosterOptionsModal from './Posters/Options/PlatformIndexPosterOptionsModal';
import PlatformIndexPosters from './Posters/PlatformIndexPosters';
import PlatformIndexSelectAllButton from './Select/PlatformIndexSelectAllButton';
import PlatformIndexSelectAllMenuItem from './Select/PlatformIndexSelectAllMenuItem';
import PlatformIndexSelectFooter from './Select/PlatformIndexSelectFooter';
import PlatformIndexSelectModeButton from './Select/PlatformIndexSelectModeButton';
import PlatformIndexSelectModeMenuItem from './Select/PlatformIndexSelectModeMenuItem';
import PlatformIndexFooter from './PlatformIndexFooter';
import PlatformIndexTable from './Table/PlatformIndexTable';
import PlatformIndexTableOptions from './Table/PlatformIndexTableOptions';
import styles from './PlatformIndex.module.css';

function getViewComponent(view: string) {
  if (view === 'posters') return PlatformIndexPosters;
  if (view === 'overview') return PlatformIndexOverviews;

  return PlatformIndexTable;
}

function getOptionsIcon(view: string) {
  if (view === 'posters') return icons.POSTER;
  if (view === 'overview') return icons.OVERVIEW;

  return icons.TABLE;
}

function PlatformIndex() {
  const platformIndex = usePlatformIndex();

  return (
    <QueueDetailsProvider all={true}>
      <SelectProvider items={platformIndex.data}>
        <PlatformIndexBody platformIndex={platformIndex} />
      </SelectProvider>
    </QueueDetailsProvider>
  );
}

interface PlatformIndexBodyProps {
  platformIndex: ReturnType<typeof usePlatformIndex>;
}

function PlatformIndexBody({ platformIndex }: PlatformIndexBodyProps) {
  const {
    isLoading: isFetching,
    isFetched,
    isError: error,
    data,
    totalItems,
  } = platformIndex;

  const { selectedFilterKey, sortKey, sortDirection, view, columns } =
    usePlatformOptions();
  const filters = FILTERS;

  const customFilters = useCustomFiltersList('series');

  const executeCommand = useExecuteCommand();
  const isRssSyncExecuting = useCommandExecuting(CommandNames.RssSync);
  const isRefreshingSeries = useCommandExecuting(CommandNames.RefreshSeries);
  const isSmallScreen = useAppDimension('isSmallScreen');
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [isOptionsModalOpen, setIsOptionsModalOpen] = useState(false);
  const [jumpToCharacter, setJumpToCharacter] = useState<string | undefined>(
    undefined
  );
  const [isSelectMode, setIsSelectMode] = useState(false);
  const { anySelected, getSelectedIds } = useSelect<Series>();

  let refreshLabel = translate('UpdateAll');

  if (anySelected) {
    refreshLabel = translate('UpdateSelected');
  } else if (selectedFilterKey !== 'all') {
    refreshLabel = translate('UpdateFiltered');
  }

  const handleRefreshSeriesPress = useCallback(() => {
    const seriesToRefresh =
      isSelectMode && anySelected ? getSelectedIds() : data.map((m) => m.id);

    executeCommand({
      name: CommandNames.RefreshSeries,
      seriesIds: seriesToRefresh,
    });
  }, [executeCommand, anySelected, isSelectMode, data, getSelectedIds]);

  const onRssSyncPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RssSync,
    });
  }, [executeCommand]);

  const onSelectModePress = useCallback(() => {
    setIsSelectMode((val) => !val);
  }, []);

  const onTableOptionChange = useCallback(
    (
      payload: TableOptionsChangePayload & {
        tableOptions?: { showBanners?: boolean; showSearchAction?: boolean };
      }
    ) => {
      if (payload.tableOptions) {
        setSeriesTableOptions(payload.tableOptions);
      } else if (payload.columns) {
        setSeriesOption('columns', payload.columns);
      }
    },
    []
  );

  const onViewSelect = useCallback(
    (value: string) => {
      setSeriesOption('view', value);

      if (scrollerRef.current) {
        scrollerRef.current.scrollTo(0, 0);
      }
    },
    [scrollerRef]
  );

  const onSortSelect = useCallback((value: string) => {
    setSeriesSort({ sortKey: value });
  }, []);

  const onFilterSelect = useCallback((value: string | number) => {
    setSeriesOption('selectedFilterKey', value);
  }, []);

  const onOptionsPress = useCallback(
    () => setIsOptionsModalOpen(true),
    [setIsOptionsModalOpen]
  );

  const onOptionsModalClose = useCallback(
    () => setIsOptionsModalOpen(false),
    [setIsOptionsModalOpen]
  );

  const onJumpBarItemPress = useCallback(
    (character: string) => {
      setJumpToCharacter(character);
    },
    [setJumpToCharacter]
  );

  const onScroll = useCallback(() => {
    setJumpToCharacter(undefined);
  }, [setJumpToCharacter]);

  const [tableOptionsModalOpen, setTableOptionsModalOpen] = useState(false);

  const handleTableOptionsPress = useCallback(
    () => setTableOptionsModalOpen(true),
    []
  );

  const handleTableOptionsModalClose = useCallback(
    () => setTableOptionsModalOpen(false),
    []
  );

  const handleOptionsTrigger = useCallback(() => {
    if (view === 'table') {
      handleTableOptionsPress();
    } else {
      onOptionsPress();
    }
  }, [view, handleTableOptionsPress, onOptionsPress]);

  const isLoaded = !!(!error && isFetched && data.length);
  const hasNoSeries = !totalItems;

  const renderSelectButton = useCallback(
    ({ label, iconName }: PageToolbarButtonProps) => (
      <PlatformIndexSelectModeButton
        label={label}
        iconName={iconName}
        isSelectMode={isSelectMode}
        onPress={onSelectModePress}
      />
    ),
    [isSelectMode, onSelectModePress]
  );

  const renderSelectOverflow = useCallback(
    ({ label, iconName }: PageToolbarButtonProps) => (
      <PlatformIndexSelectModeMenuItem
        label={label}
        iconName={iconName}
        isSelectMode={isSelectMode}
        onPress={onSelectModePress}
      />
    ),
    [isSelectMode, onSelectModePress]
  );

  const renderSelectAllButton = useCallback(
    () => <PlatformIndexSelectAllButton />,
    []
  );

  const renderSelectAllOverflow = useCallback(
    ({ label }: PageToolbarButtonProps) => (
      <PlatformIndexSelectAllMenuItem label={label} isSelectMode={isSelectMode} />
    ),
    [isSelectMode]
  );

  const jumpBarItems: PageJumpBarItems = useMemo(() => {
    // Reset if not sorting by sortTitle
    if (sortKey !== 'sortTitle') {
      return {
        characters: {},
        order: [],
      };
    }

    const characters = data.reduce((acc: Record<string, number>, item) => {
      let char = item.sortTitle.charAt(0);

      if (!isNaN(Number(char))) {
        char = '#';
      }

      if (char in acc) {
        acc[char] = acc[char] + 1;
      } else {
        acc[char] = 1;
      }

      return acc;
    }, {});

    const order = Object.keys(characters).sort();

    // Reverse if sorting descending
    if (sortDirection === DESCENDING) {
      order.reverse();
    }

    return {
      characters,
      order,
    };
  }, [data, sortKey, sortDirection]);
  const showJumpBar = isLoaded && !!jumpBarItems.order.length;

  const ViewComponent = useMemo(() => getViewComponent(view), [view]);

  return (
    <PageContent title={translate('Series')}>
      <PageToolbar>
        <ToolbarItem
          id="refresh"
          priority={1}
          groupId="left-a"
          label={refreshLabel}
          iconName={icons.REFRESH}
          isSpinning={isRefreshingSeries}
          isDisabled={hasNoSeries}
          onPress={handleRefreshSeriesPress}
        />

        <ToolbarItem
          id="rss"
          priority={1}
          groupId="left-a"
          label={translate('RssSync')}
          iconName={icons.RSS}
          isSpinning={isRssSyncExecuting}
          isDisabled={hasNoSeries}
          onPress={onRssSyncPress}
        />

        <OverflowDivider groupId="left-a">
          <PageToolbarSeparator />
        </OverflowDivider>

        <ToolbarItem
          id="select"
          priority={1}
          groupId="left-b"
          label={
            isSelectMode
              ? translate('StopSelecting')
              : translate('SelectMultipleSeries')
          }
          iconName={isSelectMode ? icons.SERIES_ENDED : icons.CHECK}
          renderButton={renderSelectButton}
          renderOverflow={renderSelectOverflow}
          onPress={onSelectModePress}
        />

        {isSelectMode ? (
          <ToolbarItem
            id="selectall"
            priority={1}
            groupId="left-b"
            label={translate('SelectAll')}
            iconName={icons.CHECK_SQUARE}
            renderButton={renderSelectAllButton}
            renderOverflow={renderSelectAllOverflow}
          />
        ) : null}

        <OverflowDivider groupId="left-b">
          <PageToolbarSeparator />
        </OverflowDivider>

        <PageToolbarSpacer />

        <ToolbarItem
          id="options"
          priority={2}
          groupId="right"
          label={translate('Options')}
          iconName={getOptionsIcon(view)}
          isDisabled={hasNoSeries}
          onPress={handleOptionsTrigger}
        />

        <OverflowDivider groupId="right">
          <PageToolbarSeparator />
        </OverflowDivider>

        <ToolbarItem id="view" pinned={true}>
          <PlatformIndexViewMenu
            view={view}
            isDisabled={hasNoSeries}
            onViewSelect={onViewSelect}
          />
        </ToolbarItem>

        <ToolbarItem id="sort" pinned={true}>
          <PlatformIndexSortMenu
            sortKey={sortKey}
            sortDirection={sortDirection}
            isDisabled={hasNoSeries}
            onSortSelect={onSortSelect}
          />
        </ToolbarItem>

        <ToolbarItem id="filter" pinned={true}>
          <PlatformIndexFilterMenu
            selectedFilterKey={selectedFilterKey}
            filters={filters}
            customFilters={customFilters}
            isDisabled={hasNoSeries}
            onFilterSelect={onFilterSelect}
          />
        </ToolbarItem>
      </PageToolbar>
      <div className={styles.pageContentBodyWrapper}>
        <PageContentBody
          ref={scrollerRef}
          className={styles.contentBody}
          innerClassName={classNames(
            (styles as unknown as Record<string, string>)[
              `${view}InnerContentBody`
            ],
            showJumpBar && styles.hasJumpBar
          )}
          scrollPositionKey="platformIndex"
          onScroll={onScroll}
        >
          {isFetching && !isFetched ? <LoadingIndicator /> : null}

          {!isFetching && !!error ? (
            <Alert kind={kinds.DANGER}>{translate('SeriesLoadError')}</Alert>
          ) : null}

          {isLoaded ? (
            <div className={styles.contentBodyContainer}>
              <ViewComponent
                scrollerRef={scrollerRef}
                items={data}
                sortKey={sortKey}
                sortDirection={sortDirection}
                jumpToCharacter={jumpToCharacter}
                isSelectMode={isSelectMode}
                isSmallScreen={isSmallScreen}
              />

              <PlatformIndexFooter />
            </div>
          ) : null}

          {!error && isFetched && !data.length ? (
            <NoPlatform totalItems={totalItems} />
          ) : null}
        </PageContentBody>
        {showJumpBar ? (
          <PageJumpBar items={jumpBarItems} onItemPress={onJumpBarItemPress} />
        ) : null}
      </div>

      {isSelectMode ? <PlatformIndexSelectFooter /> : null}

      {view === 'posters' ? (
        <PlatformIndexPosterOptionsModal
          isOpen={isOptionsModalOpen}
          onModalClose={onOptionsModalClose}
        />
      ) : null}
      {view === 'overview' ? (
        <PlatformIndexOverviewOptionsModal
          isOpen={isOptionsModalOpen}
          onModalClose={onOptionsModalClose}
        />
      ) : null}

      <TableOptionsModal
        isOpen={tableOptionsModalOpen}
        columns={columns}
        optionsComponent={PlatformIndexTableOptions}
        onTableOptionChange={onTableOptionChange}
        onModalClose={handleTableOptionsModalClose}
      />
    </PageContent>
  );
}

export default PlatformIndex;
