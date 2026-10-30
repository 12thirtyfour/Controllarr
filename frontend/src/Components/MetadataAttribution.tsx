import React from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';
import styles from './MetadataAttribution.module.css';

interface MetadataAttributionProps {
  verifiedText?: string;
  supportedFormatsText?: string;
}

export default function MetadataAttribution({
  verifiedText,
  supportedFormatsText,
}: MetadataAttributionProps) {
  return (
    <div className={styles.container}>
      <Link className={styles.attribution} to="/settings/metadatasource">
        {translate('MetadataProvidedBy', {
          provider: 'No-Intro / Redump / IGDB',
        })}
      </Link>
      {verifiedText ? (
        <div className={styles.detailText}>{verifiedText}</div>
      ) : null}
      {supportedFormatsText ? (
        <div className={styles.detailText}>{supportedFormatsText}</div>
      ) : null}
    </div>
  );
}
