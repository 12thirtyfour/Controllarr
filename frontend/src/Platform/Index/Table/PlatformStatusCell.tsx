import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import StatusIndicator from 'Components/StatusIndicator';
import VirtualTableRowCell from 'Components/Table/Cells/TableRowCell';
import { PlatformStatus } from 'Platform/Platform';
import { getPlatformStatusDetails } from 'Platform/PlatformStatus';
import { useToggleSeriesMonitored } from 'Platform/usePlatform';
import styles from './PlatformStatusCell.module.css';

interface PlatformStatusCellProps {
  className: string;
  seriesId: number;
  monitored: boolean;
  status: PlatformStatus;
  isSelectMode: boolean;
  component?: React.ElementType;
}

function PlatformStatusCell({
  className,
  seriesId,
  monitored,
  status,
  isSelectMode,
  component: Component = VirtualTableRowCell,
  ...otherProps
}: PlatformStatusCellProps) {
  const statusDetails = getPlatformStatusDetails(status);
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

      <StatusIndicator
        className={styles.statusIcon}
        label={statusDetails.message}
        title={`${statusDetails.title}: ${statusDetails.message}`}
      >
        <Icon name={statusDetails.icon} />
      </StatusIndicator>
    </Component>
  );
}

export default PlatformStatusCell;
