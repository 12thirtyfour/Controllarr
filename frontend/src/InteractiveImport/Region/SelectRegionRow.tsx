import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import formatSeason from 'Region/formatRegion';
import styles from './SelectRegionRow.module.css';

interface SelectRegionRowProps {
  seasonNumber: number;
  onSeasonSelect(season: number): unknown;
}

function SelectRegionRow(props: SelectRegionRowProps) {
  const { seasonNumber, onSeasonSelect } = props;

  const onSeasonSelectWrapper = useCallback(() => {
    onSeasonSelect(seasonNumber);
  }, [seasonNumber, onSeasonSelect]);

  return (
    <Link
      className={styles.season}
      component="div"
      onPress={onSeasonSelectWrapper}
    >
      {formatSeason(seasonNumber)}
    </Link>
  );
}

export default SelectRegionRow;
