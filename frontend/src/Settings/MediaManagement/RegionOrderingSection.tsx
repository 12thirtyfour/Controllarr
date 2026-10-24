import { move } from '@dnd-kit/helpers';
import { DragDropProvider, DragEndEvent, DragOverEvent } from '@dnd-kit/react';
import { useSortable } from '@dnd-kit/react/sortable';
import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import {
  DEFAULT_REGION_ORDER,
  normalizeRegionOrder,
  PLATFORM_REGION_ITEMS,
  PlatformRegionItem,
  setAddSeriesOption,
  useAddSeriesOption,
} from 'AddSeries/addSeriesOptionsStore';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import styles from './RegionOrderingSection.module.css';

interface SortableRegionRowProps {
  item: PlatformRegionItem;
  index: number;
}

function SortableRegionRow({ item, index }: SortableRegionRowProps) {
  const { ref, handleRef, isDragging } = useSortable({
    id: String(item.id),
    index,
  });

  return (
    <div ref={ref} className={styles.rowContainer}>
      <div className={classNames(styles.row, isDragging && styles.isDragging)}>
        <span className={styles.rankBadge}>#{index + 1}</span>
        <div className={styles.info}>
          <span className={styles.label}>{item.label}</span>
          <span className={styles.description}>{item.description}</span>
        </div>
        <div
          ref={handleRef}
          className={styles.dragHandle}
          title="Drag to reorder"
        >
          <Icon name={icons.GRIP} size={16} />
        </div>
      </div>
    </div>
  );
}

function RegionOrderingSection() {
  const storedOrder = useAddSeriesOption('regionOrder');
  const normalizedOrder = useMemo(
    () => normalizeRegionOrder(storedOrder),
    [storedOrder]
  );

  const [localIds, setLocalIds] = useState<string[] | null>(null);

  const itemsById = useMemo(
    () => new Map(PLATFORM_REGION_ITEMS.map((item) => [String(item.id), item])),
    []
  );

  const displayedItems = useMemo(() => {
    const activeIds = localIds ?? normalizedOrder.map((id) => String(id));
    return activeIds
      .map((id) => itemsById.get(id))
      .filter((item): item is PlatformRegionItem => Boolean(item));
  }, [localIds, normalizedOrder, itemsById]);

  const handleDragStart = useCallback(() => {
    setLocalIds(normalizedOrder.map((id) => String(id)));
  }, [normalizedOrder]);

  const handleDragOver = useCallback((event: DragOverEvent) => {
    setLocalIds((current) => (current ? move(current, event) : current));
  }, []);

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    setLocalIds((current) => {
      if (current && !event.canceled) {
        const nextIds = move(current, event);
        setAddSeriesOption(
          'regionOrder',
          nextIds.map((id) => Number(id))
        );
      }

      return null;
    });
  }, []);

  const handleResetDefault = useCallback(() => {
    setLocalIds(null);
    setAddSeriesOption('regionOrder', [...DEFAULT_REGION_ORDER]);
  }, []);

  return (
    <FieldSet
      legend="Platform Region & Section Ordering (Drag & Drop)"
      caption="Drag and drop the sections below to customize the display order on every Platform page and set your global 1G1R regional priority."
    >
      <div className={styles.container}>
        <div className={styles.headerRow}>
          <p className={styles.helpText}>
            Grab any row handle on the right to reorder platform sections and
            regions:
          </p>
          <button
            type="button"
            className={styles.resetButton}
            onClick={handleResetDefault}
          >
            Reset to Default Order
          </button>
        </div>

        <DragDropProvider
          onDragStart={handleDragStart}
          onDragOver={handleDragOver}
          onDragEnd={handleDragEnd}
        >
          <div className={styles.list}>
            {displayedItems.map((item, index) => (
              <SortableRegionRow key={item.id} item={item} index={index} />
            ))}
          </div>
        </DragDropProvider>
      </div>
    </FieldSet>
  );
}

export default RegionOrderingSection;
