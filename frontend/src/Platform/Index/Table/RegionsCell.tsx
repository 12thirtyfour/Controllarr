import React from 'react';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import Popover from 'Components/Tooltip/Popover';
import RegionDetails from 'Platform/Index/Select/RegionPass/RegionDetails';
import { Season } from 'Platform/Platform';
import translate from 'Utilities/String/translate';
import styles from './RegionsCell.module.css';

interface PlatformStatusCellProps {
  className: string;
  seriesId: number;
  seasonCount: number;
  seasons: Season[];
  isSelectMode: boolean;
}

function SeasonsCell(props: PlatformStatusCellProps) {
  const {
    className,
    seriesId,
    seasonCount,
    seasons,
    isSelectMode,
    ...otherProps
  } = props;

  return (
    <VirtualTableRowCell className={className} {...otherProps}>
      {isSelectMode ? (
        <Popover
          className={styles.seasonCount}
          anchor={seasonCount}
          title={translate('RegionDetails')}
          body={<RegionDetails seriesId={seriesId} seasons={seasons} />}
          position="left"
        />
      ) : (
        seasonCount
      )}
    </VirtualTableRowCell>
  );
}

export default SeasonsCell;
