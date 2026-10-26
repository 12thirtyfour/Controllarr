import classNames from 'classnames';
import React, { useCallback, useEffect, useRef } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import useExistingPlatform from 'Platform/useExistingPlatform';
import ImportPlatformTitle from './ImportPlatformTitle';
import styles from './ImportPlatformSearchResult.module.css';

interface ImportPlatformSearchResultProps {
  id: string;
  tvdbId: number;
  title: string;
  year: number;
  network?: string;
  isHighlighted: boolean;
  onPress: (tvdbId: number) => void;
}

function ImportPlatformSearchResult({
  id,
  tvdbId,
  title,
  year,
  network,
  isHighlighted,
  onPress,
}: ImportPlatformSearchResultProps) {
  const isExistingSeries = useExistingPlatform(tvdbId);

  const rowRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isHighlighted) {
      rowRef.current?.scrollIntoView({ block: 'nearest' });
    }
  }, [isHighlighted]);

  const handlePress = useCallback(() => {
    onPress(tvdbId);
  }, [tvdbId, onPress]);

  return (
    <div
      ref={rowRef}
      className={classNames(
        styles.container,
        isHighlighted ? styles.highlighted : undefined
      )}
      role="row"
      id={id}
      aria-selected={isHighlighted}
    >
      <div className={styles.seriesCell} role="gridcell">
        <Link className={styles.series} onPress={handlePress}>
          <ImportPlatformTitle
            title={title}
            year={year}
            network={network}
            isExistingSeries={isExistingSeries}
          />
        </Link>
      </div>

      <div className={styles.tvdbLinkCell} role="gridcell">
        <Link
          className={styles.tvdbLink}
          to={`https://www.igdb.com/search?q=${encodeURIComponent(title)}`}
        >
          <Icon
            className={styles.tvdbLinkIcon}
            name={icons.EXTERNAL_LINK}
            size={16}
          />
        </Link>
      </div>
    </div>
  );
}

export default ImportPlatformSearchResult;
