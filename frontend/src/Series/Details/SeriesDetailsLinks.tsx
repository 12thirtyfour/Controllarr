import React, { useMemo } from 'react';
import ClipboardButton from 'Components/Link/ClipboardButton';
import Link from 'Components/Link/Link';
import { kinds, sizes } from 'Helpers/Props';
import Series from 'Series/Series';
import translate from 'Utilities/String/translate';
import styles from './SeriesDetailsLinks.module.css';

type SeriesDetailsLinksProps = Pick<
  Series,
  'tvdbId' | 'tvMazeId' | 'imdbId' | 'tmdbId'
>;

interface SeriesDetailsLink {
  externalId?: string | number;
  name: string;
  url: string;
}

function SeriesDetailsLinks(props: SeriesDetailsLinksProps) {
  const { tvdbId } = props;

  const links = useMemo(() => {
    const validLinks: SeriesDetailsLink[] = [];

    if (tvdbId) {
      validLinks.push(
        {
          externalId: tvdbId,
          name: 'IGDB',
          url: 'https://www.igdb.com/',
        },
        {
          name: 'No-Intro',
          url: 'https://datomatic.no-intro.org/',
        },
        {
          name: 'Redump',
          url: 'http://redump.org/',
        },
        {
          name: 'RetroAchievements',
          url: 'https://retroachievements.org/',
        }
      );
    }

    return validLinks.sort(
      (a, b) => Number(!a.externalId) - Number(!b.externalId)
    );
  }, [tvdbId]);

  return (
    <div className={styles.links}>
      {links.map((link) => (
        <div key={link.name} className={styles.linkBlock}>
          <Link className={styles.link} to={link.url}>
            {link.name}
          </Link>

          {link.externalId ? (
            <ClipboardButton
              className={styles.copyButton}
              value={`${link.externalId}`}
              title={translate('CopyToClipboard')}
              kind={kinds.DEFAULT}
              size={sizes.SMALL}
              label={link.externalId}
            />
          ) : null}
        </div>
      ))}
    </div>
  );
}

export default SeriesDetailsLinks;
