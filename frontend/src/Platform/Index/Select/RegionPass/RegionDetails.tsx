import React, { useMemo } from 'react';
import { Season } from 'Platform/Platform';
import translate from 'Utilities/String/translate';
import RegionPassSeason from './RegionPassRegion';
import styles from './RegionDetails.module.css';

interface RegionDetailsProps {
  seriesId: number;
  seasons: Season[];
}

function RegionDetails(props: RegionDetailsProps) {
  const { seriesId, seasons } = props;

  const latestSeasons = useMemo(() => {
    return seasons.slice(Math.max(seasons.length - 25, 0));
  }, [seasons]);

  return (
    <div className={styles.seasons}>
      {latestSeasons.map((season) => {
        const { seasonNumber, monitored, statistics } = season;

        return (
          <RegionPassSeason
            key={seasonNumber}
            seriesId={seriesId}
            seasonNumber={seasonNumber}
            monitored={monitored}
            statistics={statistics}
          />
        );
      })}

      {latestSeasons.length < seasons.length ? (
        <div className={styles.truncated}>
          {translate('RegionPassTruncated')}
        </div>
      ) : null}
    </div>
  );
}

export default RegionDetails;
