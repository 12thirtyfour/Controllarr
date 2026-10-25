import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import StatusIndicator from 'Components/StatusIndicator';
import VirtualTableRowCell from 'Components/Table/Cells/TableRowCell';
import { SeriesStatus } from 'Series/Series';
import { getSeriesStatusDetails } from 'Series/SeriesStatus';
import { useToggleSeriesMonitored } from 'Series/useSeries';
import styles from './SeriesStatusCell.module.css';

interface SeriesStatusCellProps {
  className: string;
  seriesId: number;
  monitored: boolean;
  status: SeriesStatus;
  isSelectMode: boolean;
  component?: React.ElementType;
}

function SeriesStatusCell({
  className,
  seriesId,
  monitored,
  status,
  isSelectMode,
  component: Component = VirtualTableRowCell,
  ...otherProps
}: SeriesStatusCellProps) {
  const statusDetails = getSeriesStatusDetails(status);
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

export default SeriesStatusCell;
