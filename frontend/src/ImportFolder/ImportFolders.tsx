import React from 'react';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { kinds } from 'Helpers/Props';
import ImportFolderRow from './ImportFolderRow';
import useImportFolders from './useImportFolders';
import styles from './ImportFolders.module.css';

interface ImportFoldersProps {
  isSelectable?: boolean;
}

function ImportFolders({ isSelectable = false }: ImportFoldersProps) {
  const { isFetching, isFetched, error, data } = useImportFolders();

  if (isFetching && !isFetched) {
    return <LoadingIndicator />;
  }

  if (!isFetching && !!error) {
    return (
      <Alert kind={kinds.DANGER}>Failed to load import source folders</Alert>
    );
  }

  return (
    <div className={styles.list}>
      {data.map((importFolder) => (
        <ImportFolderRow
          key={importFolder.id}
          {...importFolder}
          isSelectable={isSelectable}
        />
      ))}
    </div>
  );
}

export default ImportFolders;
