import React, { useCallback } from 'react';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import VirtualTableRowCell from 'Components/Table/Cells/TableRowCell';
import { useToggleSeriesMonitored } from 'Platform/usePlatform';
import styles from './PlatformStatusCell.module.css';

interface PlatformStatusCellProps {
  className: string;
  seriesId: number;
  monitored: boolean;
  isSelectMode: boolean;
  component?: React.ElementType;
}

function PlatformStatusCell({
  className,
  seriesId,
  monitored,
  isSelectMode,
  component: Component = VirtualTableRowCell,
  ...otherProps
}: PlatformStatusCellProps) {
  const { toggleSeriesMonitored, isTogglingSeriesMonitored } =
    useToggleSeriesMonitored(seriesId);

  const onMonitoredPress = useCallback(() => {
    toggleSeriesMonitored({ monitored: !monitored });
  }, [monitored, toggleSeriesMonitored]);

  return (
    <Component className={className} {...otherProps}>
      <MonitorToggleButton
        className={styles.statusIcon}
        monitored={monitored}
        isSaving={isTogglingSeriesMonitored}
        onPress={onMonitoredPress}
      />
    </Component>
  );
}

export default PlatformStatusCell;
