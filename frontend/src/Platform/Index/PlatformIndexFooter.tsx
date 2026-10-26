import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import usePlatform from 'Platform/usePlatform';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './PlatformIndexFooter.module.css';

export default function PlatformIndexFooter() {
  const { data: series } = usePlatform();
  const count = series.length;
  let games = 0;
  let romFiles = 0;
  let ended = 0;
  let continuing = 0;
  let monitored = 0;
  let totalFileSize = 0;

  series.forEach((s) => {
    const {
      statistics = { gameCount: 0, romFileCount: 0, sizeOnDisk: 0 },
    } = s;

    const {
      gameCount = 0,
      romFileCount = 0,
      sizeOnDisk = 0,
    } = statistics;

    games += gameCount;
    romFiles += romFileCount;

    if (s.status === 'ended') {
      ended++;
    } else {
      continuing++;
    }

    if (s.monitored) {
      monitored++;
    }

    totalFileSize += sizeOnDisk;
  });

  return (
    <div className={styles.footer}>
      <div>
        <div className={styles.legendItem}>
          <div className={styles.continuing} />
          <div>{translate('PlatformIndexFooterContinuing')}</div>
        </div>

        <div className={styles.legendItem}>
          <div className={styles.ended} />
          <div>{translate('PlatformIndexFooterEnded')}</div>
        </div>

        <div className={styles.legendItem}>
          <div className={styles.missingMonitored} />
          <div>{translate('PlatformIndexFooterMissingMonitored')}</div>
        </div>

        <div className={styles.legendItem}>
          <div className={styles.missingUnmonitored} />
          <div>{translate('PlatformIndexFooterMissingUnmonitored')}</div>
        </div>

        <div className={styles.legendItem}>
          <div className={styles.downloading} />
          <div>{translate('PlatformIndexFooterDownloading')}</div>
        </div>
      </div>

      <div className={styles.statistics}>
        <DescriptionList>
          <DescriptionListItem title={translate('Series')} data={count} />

          <DescriptionListItem title={translate('Ended')} data={ended} />

          <DescriptionListItem
            title={translate('Continuing')}
            data={continuing}
          />
        </DescriptionList>

        <DescriptionList>
          <DescriptionListItem
            title={translate('Monitored')}
            data={monitored}
          />

          <DescriptionListItem
            title={translate('Unmonitored')}
            data={count - monitored}
          />
        </DescriptionList>

        <DescriptionList>
          <DescriptionListItem title={translate('Games')} data={games} />

          <DescriptionListItem title={translate('Files')} data={romFiles} />
        </DescriptionList>

        <DescriptionList>
          <DescriptionListItem
            title={translate('TotalFileSize')}
            data={formatBytes(totalFileSize)}
          />
        </DescriptionList>
      </div>
    </div>
  );
}
