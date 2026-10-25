import React from 'react';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import TableRowCell from './TableRowCell';
import styles from './RelativeDateCell.module.css';

interface RelativeDateCellProps {
  className?: string;
  date?: string;
  includeSeconds?: boolean;
  includeTime?: boolean;
  component?: React.ElementType;
}

function RelativeDateCell(props: RelativeDateCellProps) {
  const {
    className = styles.cell,
    date,
    includeSeconds = false,
    includeTime = false,
    component: Component = TableRowCell,
    ...otherProps
  } = props;

  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();

  const isInvalidYear = Boolean(date && (parseInt(date.slice(0, 4), 10) < 1970 || date.startsWith('189')));
  if (!date || isInvalidYear) {
    return <Component className={className} {...otherProps} />;
  }

  return (
    <Component
      className={className}
      title={formatDateTime(date, longDateFormat, timeFormat, {
        includeSeconds,
        includeRelativeDay: !showRelativeDates,
      })}
      {...otherProps}
    >
      {getRelativeDate({
        date,
        shortDateFormat,
        showRelativeDates,
        timeFormat,
        includeSeconds,
        includeTime,
        timeForToday: true,
      })}
    </Component>
  );
}

export default RelativeDateCell;
