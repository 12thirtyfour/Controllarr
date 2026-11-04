import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import PlatformPoster from 'Platform/PlatformPoster';
import { Tag } from 'Tags/useTags';
import { SuggestedSeries } from './PlatformSearchInput';
import styles from './PlatformSearchResult.module.css';

interface Match {
  key: string;
  refIndex: number;
}

interface PlatformSearchResultProps extends SuggestedSeries {
  match: Match;
}

function PlatformSearchResult(props: PlatformSearchResultProps) {
  const {
    match,
    title,
    images,
    alternateTitles,
    tvdbId,
    tags,
  } = props;

  let alternateTitle = null;
  let tag: Tag | null = null;

  if (match.key === 'alternateTitles.title') {
    alternateTitle = alternateTitles[match.refIndex];
  } else if (match.key === 'tags.label') {
    tag = tags[match.refIndex];
  }

  return (
    <div className={styles.result}>
      <PlatformPoster
        className={styles.poster}
        images={images}
        size={250}
        lazy={false}
        overflow={true}
        title={title}
      />

      <div className={styles.titles}>
        <div className={styles.title}>{title}</div>

        {alternateTitle ? (
          <div className={styles.alternateTitle}>{alternateTitle.title}</div>
        ) : null}

        {match.key === 'tvdbId' && tvdbId ? (
          <div className={styles.alternateTitle}>Catalog ID: {tvdbId}</div>
        ) : null}

        {tag ? (
          <div className={styles.tagContainer}>
            <Label key={tag.id} kind={kinds.INFO}>
              {tag.label}
            </Label>
          </div>
        ) : null}
      </div>
    </div>
  );
}

export default PlatformSearchResult;
