import { throttle } from 'lodash';
import React, {
  RefObject,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { ListChildComponentProps, VariableSizeList } from 'react-window';
import { useAppDimensions } from 'App/appStore';
import { useSelect } from 'App/Select/SelectContext';
import useMeasure from 'Helpers/Hooks/useMeasure';
import usePlatform from 'Platform/usePlatform';
import ImportPlatformCard from './ImportPlatformCard';
import {
  getImportPlatformEligibility,
  getImportPlatformSelection,
  ImportPlatformEligibility,
} from './importPlatformEligibility';
import {
  ImportPlatformItem,
  UnamppedFolderItem,
  useEnsureImportPlatformItems,
  useImportPlatformItems,
  useImportPlatformViewOption,
} from './importPlatformStore';
import styles from './ImportPlatformList.module.css';

const ITEM_HEIGHT = 112;
const ITEM_HEIGHT_COMPACT = 64;
const ITEM_HEIGHT_STACKED = 261;
const ITEM_HEIGHT_STACKED_UNMATCHED = 80;
const STACKED_WIDTH = 960;

interface Row {
  duplicateIds: string[];
  eligibility: ImportPlatformEligibility;
  isReady: boolean;
  item: UnamppedFolderItem;
}

interface ItemData {
  rows: Row[];
  isCompact: boolean;
  isStacked: boolean;
}

interface ImportPlatformListProps {
  items: UnamppedFolderItem[];
  scrollerRef: RefObject<HTMLElement>;
}

function getWindowScrollTopPosition() {
  return document.documentElement.scrollTop || document.body.scrollTop || 0;
}

function Item({ index, style, data }: ListChildComponentProps<ItemData>) {
  const { rows, isCompact, isStacked } = data;
  const row = rows[index];

  if (!row) {
    return null;
  }

  return (
    <div style={style} className={styles.item}>
      <ImportPlatformCard
        unmappedFolder={row.item}
        duplicateIds={row.duplicateIds}
        eligibility={row.eligibility}
        isCompact={isCompact}
        isStacked={isStacked}
      />
    </div>
  );
}

function ImportPlatformList({ items, scrollerRef }: ImportPlatformListProps) {
  const {
    width: windowWidth,
    height: windowHeight,
    isSmallScreen,
  } = useAppDimensions();
  const { setItemStates, useHasItems, useSelectedIds } =
    useSelect<ImportPlatformItem>();
  const selectedIds = useSelectedIds();
  const compactRows = useImportPlatformViewOption('compactRows');
  const importPlatformItems = useImportPlatformItems();
  const { data: existingSeries = [] } = usePlatform();

  const listRef = useRef<VariableSizeList<ItemData>>(null);
  const readyIdsRef = useRef(new Set<string>());
  const [measureRef, bounds] = useMeasure();
  const [size, setSize] = useState({ width: 0, height: 0 });

  const hasSelectItems = useHasItems();

  useEnsureImportPlatformItems(items);

  const existingTvdbIds = useMemo(() => {
    return new Set(existingSeries.map((series) => series.tvdbId));
  }, [existingSeries]);

  const importPlatformItemById = useMemo(() => {
    return new Map(importPlatformItems.map((item) => [item.id, item]));
  }, [importPlatformItems]);

  const eligibility = useMemo(
    () => getImportPlatformEligibility(importPlatformItems, existingTvdbIds),
    [importPlatformItems, existingTvdbIds]
  );

  const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);

  const rows = useMemo(() => {
    return items.map((item) => {
      const importPlatformItem = importPlatformItemById.get(item.id);
      const tvdbId = importPlatformItem?.selectedSeries?.tvdbId;
      const status = eligibility.eligibilityById.get(item.id) ?? 'unmatched';

      return {
        item,
        duplicateIds:
          tvdbId == null
            ? []
            : eligibility.duplicateIdsByTvdbId.get(tvdbId) ?? [],
        eligibility: status,
        isReady:
          status === 'ready' ||
          (status === 'duplicate' && selectedIdSet.has(item.id)),
      };
    });
  }, [items, importPlatformItemById, eligibility, selectedIdSet]);

  useEffect(() => {
    const nextReadyIds = new Set<string>();
    const { duplicateIdsToDeselect } = getImportPlatformSelection(
      eligibility,
      selectedIds
    );

    const updates = rows.map(({ item, eligibility }) => {
      if (eligibility === 'ready') {
        nextReadyIds.add(item.id);

        return {
          id: item.id,
          isDisabled: false,
          isSelected: readyIdsRef.current.has(item.id) ? undefined : true,
        };
      }

      if (eligibility === 'duplicate') {
        return {
          id: item.id,
          isDisabled: false,
          isSelected: duplicateIdsToDeselect.has(item.id) ? false : undefined,
        };
      }

      return {
        id: item.id,
        isDisabled: true,
        isSelected: false,
      };
    });

    setItemStates(updates);
    readyIdsRef.current = nextReadyIds;
  }, [rows, eligibility, selectedIds, setItemStates]);

  const isStacked = size.width > 0 && size.width < STACKED_WIDTH;
  const isCompact = compactRows && !isStacked;

  const getItemSize = useCallback(
    (index: number) => {
      if (isCompact) {
        return ITEM_HEIGHT_COMPACT;
      }

      if (isStacked) {
        return rows[index]?.isReady
          ? ITEM_HEIGHT_STACKED
          : ITEM_HEIGHT_STACKED_UNMATCHED;
      }

      return ITEM_HEIGHT;
    },
    [rows, isCompact, isStacked]
  );

  const getItemKey = useCallback(
    (index: number, data: ItemData) => data.rows[index]?.item.id ?? index,
    []
  );

  useEffect(() => {
    listRef.current?.resetAfterIndex(0, true);
  }, [rows, isCompact, isStacked]);

  useEffect(() => {
    if (isSmallScreen) {
      setSize({
        width: windowWidth,
        height: windowHeight,
      });

      return;
    }

    if (bounds.width) {
      setSize({
        width: bounds.width,
        height: windowHeight,
      });
    }
  }, [isSmallScreen, windowWidth, windowHeight, bounds]);

  useEffect(() => {
    const currentScrollerRef = scrollerRef.current as HTMLElement;
    const currentScrollListener = isSmallScreen ? window : currentScrollerRef;

    const handleScroll = throttle(() => {
      const { offsetTop = 0 } = currentScrollerRef;
      const scrollTop =
        (isSmallScreen
          ? getWindowScrollTopPosition()
          : currentScrollerRef.scrollTop) - offsetTop;

      listRef.current?.scrollTo(scrollTop);
    }, 10);

    currentScrollListener.addEventListener('scroll', handleScroll);

    return () => {
      handleScroll.cancel();

      if (currentScrollListener) {
        currentScrollListener.removeEventListener('scroll', handleScroll);
      }
    };
  }, [isSmallScreen, listRef, scrollerRef]);

  if (!items.length || !hasSelectItems) {
    return null;
  }

  return (
    <div ref={measureRef}>
      <VariableSizeList<ItemData>
        ref={listRef}
        style={{
          width: '100%',
          height: '100%',
          overflow: 'none',
        }}
        width={size.width}
        height={size.height}
        itemCount={rows.length}
        itemKey={getItemKey}
        itemSize={getItemSize}
        estimatedItemSize={isStacked ? ITEM_HEIGHT_STACKED : ITEM_HEIGHT}
        itemData={{
          rows,
          isCompact,
          isStacked,
        }}
        overscanCount={20}
      >
        {Item}
      </VariableSizeList>
    </div>
  );
}

export default ImportPlatformList;
