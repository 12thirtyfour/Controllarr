import React, {
  ReactElement,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { SelectProvider, useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import PageMessage from 'Components/Page/PageMessage';
import { OverflowDivider } from 'Components/Page/Toolbar/Overflow';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import SectionHeading from 'Components/SectionHeading';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableOptionsModal from 'Components/Table/TableOptions/TableOptionsModal';
import TablePager from 'Components/Table/TablePager';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import useGames from 'Game/useGames';
import { align, icons, kinds } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import { CheckInputChanged } from 'typings/inputs';
import QueueModel from 'typings/Queue';
import { TableOptionsChangePayload } from 'typings/Table';
import selectUniqueIds from 'Utilities/Object/selectUniqueIds';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import translate from 'Utilities/String/translate';
import QueueFilterModal from './QueueFilterModal';
import {
  setIaQueueSort,
  setQueueOption,
  setQueueOptions,
  setQueueSort,
  useQueueOptions,
} from './queueOptionsStore';
import QueueRow from './QueueRow';
import RemoveQueueItemModal from './RemoveQueueItemModal';
import useQueueStatus from './Status/useQueueStatus';
import useQueue, {
  QueueReorderAction,
  useFilters,
  useGrabQueueItems,
  useRemoveQueueItems,
  useReorderQueueItems,
} from './useQueue';
import styles from './Queue.module.css';

function QueueContent() {
  const executeCommand = useExecuteCommand();

  const standardQueue = useQueue({ isInternetArchive: false });
  const iaQueue = useQueue({ isInternetArchive: true });

  const standardRecords = standardQueue.records;
  const iaRecords = iaQueue.records;

  const allRecords = useMemo(() => {
    return [...standardRecords, ...iaRecords];
  }, [standardRecords, iaRecords]);

  const {
    columns,
    pageSize,
    sortKey,
    sortDirection,
    iaSortKey,
    iaSortDirection,
    selectedFilterKey,
  } = useQueueOptions();

  const standardColumns = useMemo(() => {
    return columns.filter((column) => column.name !== 'queuePosition');
  }, [columns]);

  const filters = useFilters();

  const { isRemoving, removeQueueItems } = useRemoveQueueItems();
  const { isGrabbing, grabQueueItems } = useGrabQueueItems();
  const { isReordering, reorderQueueItems } = useReorderQueueItems();

  const { count } = useQueueStatus();

  const gameIds = useMemo(() => {
    return selectUniqueIds<QueueModel, number>(allRecords, 'gameIds');
  }, [allRecords]);

  const {
    isFetching: isGamesFetching,
    isFetched: isGamesFetched,
    error: gamesError,
  } = useGames({ gameIds });

  const customFilters = useCustomFiltersList('queue');

  const isRefreshMonitoredDownloadsExecuting = useCommandExecuting(
    CommandNames.RefreshMonitoredDownloads
  );

  const shouldBlockRefresh = useRef(false);
  const currentQueue = useRef<ReactElement | null>(null);
  const hasPopulatedOnce = useRef(false);

  const { setItemStates, useSelectedIds } = useSelect<QueueModel>();

  const selectedIds = useSelectedIds();
  const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);

  const standardAllSelected = useMemo(() => {
    return (
      standardRecords.length > 0 &&
      standardRecords.every((item) => selectedIdSet.has(item.id))
    );
  }, [standardRecords, selectedIdSet]);

  const standardAllUnselected = useMemo(() => {
    return standardRecords.every((item) => !selectedIdSet.has(item.id));
  }, [standardRecords, selectedIdSet]);

  const iaAllSelected = useMemo(() => {
    return (
      iaRecords.length > 0 &&
      iaRecords.every((item) => selectedIdSet.has(item.id))
    );
  }, [iaRecords, selectedIdSet]);

  const iaAllUnselected = useMemo(() => {
    return iaRecords.every((item) => !selectedIdSet.has(item.id));
  }, [iaRecords, selectedIdSet]);

  const isPendingSelected = useMemo(() => {
    return allRecords.some((item) => {
      return selectedIdSet.has(item.id) && item.status === 'delay';
    });
  }, [allRecords, selectedIdSet]);

  const completedDownloadIds = useMemo(
    () =>
      selectedIds
        .map((id) => {
          const item = allRecords.find(
            (i) => i.id === id && i.status === 'completed'
          );

          return item?.downloadId;
        })
        .filter((id): id is string => !!id),
    [allRecords, selectedIds]
  );

  const hasIaReorderableItems = useMemo(() => {
    return iaRecords.some((item) => item.canReorder);
  }, [iaRecords]);

  const maxIaQueuePosition = useMemo(() => {
    return iaRecords.reduce((max, item) => {
      return item.queuePosition != null && item.queuePosition > max
        ? item.queuePosition
        : max;
    }, 0);
  }, [iaRecords]);

  const selectedReorderableIds = useMemo(() => {
    return allRecords
      .filter((item) => selectedIdSet.has(item.id) && item.canReorder)
      .map((item) => item.id);
  }, [allRecords, selectedIdSet]);

  const canReorderSelected =
    selectedIds.length > 0 &&
    selectedReorderableIds.length === selectedIds.length;

  const [isConfirmRemoveModalOpen, setIsConfirmRemoveModalOpen] =
    useState(false);

  const [isInteractiveImportDownloadIds, setIsInteractiveImportDownloadIds] =
    useState<string[]>(() => []);

  const isLoading = standardQueue.isLoading || iaQueue.isLoading;
  const isRefreshing =
    isLoading || isGamesFetching || isRefreshMonitoredDownloadsExecuting;

  // Use isLoading over isFetched and keep table mounted after first load to avoid blinking on background queue updates
  const isPopulatedNow =
    !isLoading &&
    (isGamesFetched ||
      !allRecords.length ||
      allRecords.every((e) => !e.gameIds?.length));

  if (isPopulatedNow) {
    hasPopulatedOnce.current = true;
  }

  const isAllPopulated = hasPopulatedOnce.current || isPopulatedNow;
  const hasError = standardQueue.error || iaQueue.error || gamesError;
  const hasStandardRecords = standardRecords.length > 0;
  const hasIaRecords = iaRecords.length > 0;
  const hasAnyRecords = hasStandardRecords || hasIaRecords;
  const selectedCount = selectedIds.length;
  const disableSelectedActions = selectedCount === 0;
  const disableImportSelected =
    selectedCount === 0 || completedDownloadIds.length === 0;
  const disableReorderSelected = !canReorderSelected || isReordering;

  const handleStandardSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setItemStates(
        standardRecords.map((item) => ({
          id: item.id,
          isSelected: !!value,
        }))
      );
    },
    [setItemStates, standardRecords]
  );

  const handleIaSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setItemStates(
        iaRecords.map((item) => ({
          id: item.id,
          isSelected: !!value,
        }))
      );
    },
    [setItemStates, iaRecords]
  );

  const handleRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshMonitoredDownloads,
    });
  }, [executeCommand]);

  const handleQueueRowModalOpenOrClose = useCallback((isOpen: boolean) => {
    shouldBlockRefresh.current = isOpen;
  }, []);

  const handleGrabSelectedPress = useCallback(() => {
    grabQueueItems({ ids: selectedIds });
  }, [selectedIds, grabQueueItems]);

  const handleReorder = useCallback(
    (ids: number[], action: QueueReorderAction) => {
      if (!ids.length) {
        return;
      }

      reorderQueueItems({ ids, action });

      if (iaSortKey !== 'queuePosition' || iaSortDirection !== 'ascending') {
        setIaQueueSort({
          sortKey: 'queuePosition',
          sortDirection: 'ascending',
        });
      }
    },
    [reorderQueueItems, iaSortKey, iaSortDirection]
  );

  const handleRowReorderPress = useCallback(
    (id: number, action: QueueReorderAction) => {
      handleReorder([id], action);
    },
    [handleReorder]
  );

  const handleMoveTopSelectedPress = useCallback(() => {
    handleReorder(selectedReorderableIds, 'top');
  }, [handleReorder, selectedReorderableIds]);

  const handleMoveUpSelectedPress = useCallback(() => {
    handleReorder(selectedReorderableIds, 'up');
  }, [handleReorder, selectedReorderableIds]);

  const handleMoveDownSelectedPress = useCallback(() => {
    handleReorder(selectedReorderableIds, 'down');
  }, [handleReorder, selectedReorderableIds]);

  const handleMoveBottomSelectedPress = useCallback(() => {
    handleReorder(selectedReorderableIds, 'bottom');
  }, [handleReorder, selectedReorderableIds]);

  const handleRemoveSelectedPress = useCallback(() => {
    shouldBlockRefresh.current = true;
    setIsConfirmRemoveModalOpen(true);
  }, [setIsConfirmRemoveModalOpen]);

  const handleRemoveSelectedConfirmed = useCallback(() => {
    shouldBlockRefresh.current = false;
    removeQueueItems({ ids: selectedIds });
    setIsConfirmRemoveModalOpen(false);
  }, [selectedIds, removeQueueItems]);

  const handleConfirmRemoveModalClose = useCallback(() => {
    shouldBlockRefresh.current = false;
    setIsConfirmRemoveModalOpen(false);
  }, []);

  const handleImportSelectedPress = useCallback(() => {
    shouldBlockRefresh.current = true;
    setIsInteractiveImportDownloadIds(completedDownloadIds);
  }, [completedDownloadIds]);

  const handleImportSelectedModalClose = useCallback(() => {
    shouldBlockRefresh.current = false;
    setIsInteractiveImportDownloadIds([]);
  }, []);

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      setQueueOption('selectedFilterKey', selectedFilterKey);
    },
    []
  );

  const handleSortPress = useCallback(
    (nextSortKey: string, nextSortDirection?: SortDirection) => {
      setQueueSort({
        sortKey: nextSortKey,
        sortDirection: nextSortDirection,
      });
    },
    []
  );

  const handleIaSortPress = useCallback(
    (nextSortKey: string, nextSortDirection?: SortDirection) => {
      setIaQueueSort({
        sortKey: nextSortKey,
        sortDirection: nextSortDirection,
      });
    },
    []
  );

  const handleTableOptionChange = useCallback(
    (payload: TableOptionsChangePayload) => {
      setQueueOptions(payload);

      if (payload.pageSize) {
        standardQueue.goToPage(1);
        iaQueue.goToPage(1);
      }
    },
    [standardQueue, iaQueue]
  );

  const [isTableOptionsModalOpen, setIsTableOptionsModalOpen] = useState(false);

  const handleTableOptionsPress = useCallback(() => {
    setIsTableOptionsModalOpen(true);
  }, []);

  const handleTableOptionsModalClose = useCallback(() => {
    setIsTableOptionsModalOpen(false);
  }, []);

  const refetchStandard = standardQueue.refetch;
  const refetchIa = iaQueue.refetch;

  useEffect(() => {
    const repopulate = () => {
      refetchStandard();
      refetchIa();
    };

    registerPagePopulator(repopulate);

    return () => {
      unregisterPagePopulator(repopulate);
    };
  }, [refetchStandard, refetchIa]);

  const isIaLastPage = iaQueue.page >= iaQueue.totalPages;

  if (!shouldBlockRefresh.current) {
    currentQueue.current = (
      <PageContentBody>
        <PageHeading scope={translate('Activity')} title={translate('Queue')} />

        {isRefreshing && !isAllPopulated ? <LoadingIndicator /> : null}

        {!isRefreshing && hasError ? (
          <Alert kind={kinds.DANGER}>{translate('QueueLoadError')}</Alert>
        ) : null}

        {isAllPopulated && !hasError && !hasAnyRecords ? (
          <PageMessage>
            {selectedFilterKey !== 'all' && count > 0
              ? translate('QueueFilterHasNoItems')
              : translate('QueueIsEmpty')}
          </PageMessage>
        ) : null}

        {isAllPopulated && !hasError && hasAnyRecords ? (
          <div>
            {hasStandardRecords ? (
              <div className={styles.queueSection}>
                <SectionHeading title={translate('TorrentsAndUsenetQueue')} />

                <Table
                  selectAll={true}
                  allSelected={standardAllSelected}
                  allUnselected={standardAllUnselected}
                  columns={standardColumns}
                  pageSize={pageSize}
                  sortKey={sortKey}
                  sortDirection={sortDirection}
                  onTableOptionChange={handleTableOptionChange}
                  onSelectAllChange={handleStandardSelectAllChange}
                  onSortPress={handleSortPress}
                >
                  <TableBody>
                    {standardRecords.map((item) => {
                      return (
                        <QueueRow
                          key={item.id}
                          columns={standardColumns}
                          {...item}
                          onQueueRowModalOpenOrClose={
                            handleQueueRowModalOpenOrClose
                          }
                        />
                      );
                    })}
                  </TableBody>
                </Table>

                <TablePager
                  page={standardQueue.page}
                  totalPages={standardQueue.totalPages}
                  totalRecords={standardQueue.totalRecords}
                  isFetching={standardQueue.isFetching}
                  onPageSelect={standardQueue.goToPage}
                />
              </div>
            ) : null}

            {hasIaRecords ? (
              <div className={styles.queueSection}>
                <SectionHeading
                  title={translate('InternetArchiveQueue')}
                  description={translate('InternetArchiveQueueDescription')}
                />

                <Table
                  selectAll={true}
                  allSelected={iaAllSelected}
                  allUnselected={iaAllUnselected}
                  columns={columns}
                  pageSize={pageSize}
                  sortKey={iaSortKey}
                  sortDirection={iaSortDirection}
                  onTableOptionChange={handleTableOptionChange}
                  onSelectAllChange={handleIaSelectAllChange}
                  onSortPress={handleIaSortPress}
                >
                  <TableBody>
                    {iaRecords.map((item) => {
                      const isLastQueued =
                        isIaLastPage &&
                        item.queuePosition != null &&
                        item.queuePosition === maxIaQueuePosition;

                      return (
                        <QueueRow
                          key={item.id}
                          columns={columns}
                          {...item}
                          isLastQueued={isLastQueued}
                          isReordering={isReordering}
                          onQueueRowModalOpenOrClose={
                            handleQueueRowModalOpenOrClose
                          }
                          onReorderPress={handleRowReorderPress}
                        />
                      );
                    })}
                  </TableBody>
                </Table>

                <TablePager
                  page={iaQueue.page}
                  totalPages={iaQueue.totalPages}
                  totalRecords={iaQueue.totalRecords}
                  isFetching={iaQueue.isFetching}
                  onPageSelect={iaQueue.goToPage}
                />
              </div>
            ) : null}
          </div>
        ) : null}
      </PageContentBody>
    );
  }

  return (
    <PageContent title={translate('Queue')}>
      <PageToolbar>
        <ToolbarItem
          id="refresh"
          priority={1}
          groupId="left-a"
          label={translate('Refresh')}
          iconName={icons.REFRESH}
          isSpinning={isRefreshing}
          onPress={handleRefreshPress}
        />

        <OverflowDivider groupId="left-a">
          <PageToolbarSeparator />
        </OverflowDivider>

        <ToolbarItem
          id="grab-selected"
          priority={1}
          groupId="left-b"
          label={translate('GrabSelected')}
          iconName={icons.DOWNLOAD}
          isDisabled={disableSelectedActions || !isPendingSelected}
          isSpinning={isGrabbing}
          onPress={handleGrabSelectedPress}
        />

        <ToolbarItem
          id="remove-selected"
          priority={1}
          groupId="left-b"
          label={translate('RemoveSelected')}
          iconName={icons.REMOVE}
          isDisabled={disableSelectedActions}
          isSpinning={isRemoving}
          onPress={handleRemoveSelectedPress}
        />

        <OverflowDivider groupId="left-b">
          <PageToolbarSeparator />
        </OverflowDivider>

        <ToolbarItem
          id="import-selected"
          priority={1}
          groupId="left-c"
          label={translate('ImportSelected')}
          iconName={icons.INTERACTIVE}
          isDisabled={disableImportSelected}
          onPress={handleImportSelectedPress}
        />

        {hasIaReorderableItems ? (
          <>
            <OverflowDivider groupId="left-c">
              <PageToolbarSeparator />
            </OverflowDivider>

            <ToolbarItem
              id="move-top-selected"
              priority={2}
              groupId="left-d"
              label={translate('MoveToTop')}
              iconName={icons.MOVE_TOP}
              isDisabled={disableReorderSelected}
              onPress={handleMoveTopSelectedPress}
            />

            <ToolbarItem
              id="move-up-selected"
              priority={2}
              groupId="left-d"
              label={translate('MoveUp')}
              iconName={icons.MOVE_UP}
              isDisabled={disableReorderSelected}
              onPress={handleMoveUpSelectedPress}
            />

            <ToolbarItem
              id="move-down-selected"
              priority={2}
              groupId="left-d"
              label={translate('MoveDown')}
              iconName={icons.MOVE_DOWN}
              isDisabled={disableReorderSelected}
              onPress={handleMoveDownSelectedPress}
            />

            <ToolbarItem
              id="move-bottom-selected"
              priority={2}
              groupId="left-d"
              label={translate('MoveToBottom')}
              iconName={icons.MOVE_BOTTOM}
              isDisabled={disableReorderSelected}
              onPress={handleMoveBottomSelectedPress}
            />
          </>
        ) : null}

        <PageToolbarSpacer />

        <ToolbarItem
          id="options"
          priority={2}
          groupId="right"
          label={translate('Options')}
          iconName={icons.TABLE}
          onPress={handleTableOptionsPress}
        />

        <ToolbarItem id="filter" pinned={true}>
          <FilterMenu
            alignMenu={align.RIGHT}
            selectedFilterKey={selectedFilterKey}
            filters={filters}
            customFilters={customFilters}
            filterModalConnectorComponent={QueueFilterModal}
            onFilterSelect={handleFilterSelect}
          />
        </ToolbarItem>
      </PageToolbar>

      {currentQueue.current}

      <RemoveQueueItemModal
        isOpen={isConfirmRemoveModalOpen}
        selectedCount={selectedCount}
        canChangeCategory={
          isConfirmRemoveModalOpen &&
          selectedIds.every((id: number) => {
            const item = allRecords.find((i) => i.id === id);

            return !!(item && item.downloadClientHasPostImportCategory);
          })
        }
        canIgnore={
          isConfirmRemoveModalOpen &&
          selectedIds.every((id: number) => {
            const item = allRecords.find((i) => i.id === id);

            return !!(item && item.seriesId && item.gameIds.length);
          })
        }
        isPending={
          isConfirmRemoveModalOpen &&
          selectedIds.every((id: number) => {
            const item = allRecords.find((i) => i.id === id);

            if (!item) {
              return false;
            }

            return (
              item.status === 'delay' ||
              item.status === 'downloadClientUnavailable'
            );
          })
        }
        onRemovePress={handleRemoveSelectedConfirmed}
        onModalClose={handleConfirmRemoveModalClose}
      />

      <InteractiveImportModal
        isOpen={isInteractiveImportDownloadIds.length > 0}
        downloadIds={isInteractiveImportDownloadIds}
        title={translate('InteractiveImportMultipleQueueItems')}
        onModalClose={handleImportSelectedModalClose}
      />

      <TableOptionsModal
        isOpen={isTableOptionsModalOpen}
        columns={columns}
        pageSize={pageSize}
        maxPageSize={200}
        onTableOptionChange={handleTableOptionChange}
        onModalClose={handleTableOptionsModalClose}
      />
    </PageContent>
  );
}

function Queue() {
  const standardQueue = useQueue({ isInternetArchive: false });
  const iaQueue = useQueue({ isInternetArchive: true });

  const allRecords = useMemo(() => {
    return [...standardQueue.records, ...iaQueue.records];
  }, [standardQueue.records, iaQueue.records]);

  return (
    <SelectProvider<QueueModel> items={allRecords}>
      <QueueContent />
    </SelectProvider>
  );
}

export default Queue;
