import React, { useCallback, useState } from 'react';
import AddAllPlatformsModal from 'AddPlatform/AddNewPlatform/AddAllPlatformsModal';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './NoPlatform.module.css';

interface NoPlatformProps {
  totalItems: number;
}

function NoPlatform(props: NoPlatformProps) {
  const { totalItems } = props;
  const [isAddAllModalOpen, setIsAddAllModalOpen] = useState(false);

  const handleAddAllPlatformsPress = useCallback(() => {
    setIsAddAllModalOpen(true);
  }, []);

  const handleAddAllModalClose = useCallback(() => {
    setIsAddAllModalOpen(false);
  }, []);

  if (totalItems > 0) {
    return (
      <div>
        <div className={styles.message}>
          {translate('AllSeriesAreHiddenByTheAppliedFilter')}
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className={styles.message}>
        {translate('NoSeriesFoundImportOrAdd')}
      </div>

      <div className={styles.buttonContainer}>
        <Button to="/add/import" kind={kinds.PRIMARY}>
          {translate('ImportExistingSeries')}
        </Button>
      </div>

      <div className={styles.buttonContainer}>
        <Button to="/add/new" kind={kinds.PRIMARY}>
          {translate('AddNewPlatform')}
        </Button>
      </div>

      <div className={styles.buttonContainer}>
        <Button kind={kinds.PRIMARY} onPress={handleAddAllPlatformsPress}>
          Add All Supported Platforms
        </Button>
      </div>

      <AddAllPlatformsModal
        isOpen={isAddAllModalOpen}
        onModalClose={handleAddAllModalClose}
      />
    </div>
  );
}

export default NoPlatform;
