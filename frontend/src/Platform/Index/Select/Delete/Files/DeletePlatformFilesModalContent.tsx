import React, { useCallback } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import PlatformDeleteList from '../PlatformDeleteList';
import useSelectedPlatformStats from '../useSelectedPlatformStats';
import styles from './DeletePlatformFilesModalContent.module.css';

export interface DeletePlatformFilesModalContentProps {
  onModalClose(): void;
}

function DeletePlatformFilesModalContent({
  onModalClose,
}: DeletePlatformFilesModalContentProps) {
  const { series, seriesIds, totalRomFileCount, totalSizeOnDisk } =
    useSelectedPlatformStats();
  const executeCommand = useExecuteCommand();

  const onDeletePlatformConfirmed = useCallback(() => {
    executeCommand({
      name: CommandNames.DeletePlatformFiles,
      seriesIds,
    });

    onModalClose();
  }, [seriesIds, executeCommand, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteSelectedSeriesFiles')}</ModalHeader>

      <ModalBody>
        <div className={styles.message}>
          {translate('DeletePlatformFilesConfirmation', {
            count: series.length,
          })}
        </div>

        <PlatformDeleteList
          series={series}
          showFileDetails={true}
          totalRomFileCount={totalRomFileCount}
          totalSizeOnDisk={totalSizeOnDisk}
          styles={styles}
        />
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.DANGER} onPress={onDeletePlatformConfirmed}>
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default DeletePlatformFilesModalContent;
