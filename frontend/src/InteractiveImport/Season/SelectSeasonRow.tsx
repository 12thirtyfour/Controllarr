import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import formatSeason from 'Season/formatSeason';
import styles from './SelectSeasonRow.module.css';

interface SelectSeasonRowProps {
  seasonNumber: number;
  onSeasonSelect(season: number): unknown;
}

function SelectSeasonRow(props: SelectSeasonRowProps) {
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

export default SelectSeasonRow;
