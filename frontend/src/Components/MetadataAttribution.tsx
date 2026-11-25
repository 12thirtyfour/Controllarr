import React from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';
import styles from './MetadataAttribution.module.css';

interface MetadataAttributionProps {
  verificationDatabases?: string[];
  verificationMethods?: string[];
}

export default function MetadataAttribution({
  verificationDatabases,
  verificationMethods,
}: MetadataAttributionProps) {
  const databases = (verificationDatabases ?? []).filter(
    (item) => item && item.trim().length > 0
  );
  const methods = (verificationMethods ?? []).filter(
    (item) => item && item.trim().length > 0
  );

  let attributionText = translate('PlatformVerificationSummary', {
    databases: 'No-Intro / Redump / SMDB',
    methods: 'CRC32, MD5, SHA-1, SHA-256',
  });

  if (databases.length > 0 && methods.length > 0) {
    attributionText = translate('PlatformVerificationSummary', {
      databases: databases.join(' / '),
      methods: methods.join(', '),
    });
  } else if (databases.length > 0) {
    attributionText = translate('PlatformVerificationDatabasesOnly', {
      databases: databases.join(' / '),
    });
  }

  return (
    <div className={styles.container}>
      <Link className={styles.attribution} to="/settings/metadatasource">
        {attributionText}
      </Link>
    </div>
  );
}
