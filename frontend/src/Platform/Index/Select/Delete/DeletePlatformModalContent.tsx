import React, { useCallback, useState } from 'react';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import {
  setSeriesDeleteOptions,
  usePlatformDeleteOptions,
} from 'Platform/platformOptionsStore';
import { useBulkDeletePlatform } from 'Platform/usePlatform';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import PlatformDeleteList from './PlatformDeleteList';
import useSelectedPlatformStats from './useSelectedPlatformStats';
import styles from './DeletePlatformModalContent.module.css';

export interface DeletePlatformModalContentProps {
  onModalClose(): void;
}

function DeletePlatformModalContent({
  onModalClose,
}: DeletePlatformModalContentProps) {
  const { addImportListExclusion } = usePlatformDeleteOptions();
  const { bulkDeletePlatform } = useBulkDeletePlatform();
  const [deleteFiles, setDeleteFiles] = useState(false);
  const { series, seriesIds, totalRomFileCount, totalSizeOnDisk } =
    useSelectedPlatformStats();

  const onDeleteFilesChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setDeleteFiles(value);
    },
    [setDeleteFiles]
  );

  const onDeleteOptionChange = useCallback(
    ({ name, value }: { name: string; value: boolean }) => {
      setSeriesDeleteOptions({
        [name]: value,
      });
    },
    []
  );

  const onDeletePlatformConfirmed = useCallback(() => {
    setDeleteFiles(false);

    bulkDeletePlatform({
      seriesIds,
      deleteFiles,
      addImportListExclusion,
    });

    onModalClose();
  }, [
    deleteFiles,
    addImportListExclusion,
    setDeleteFiles,
    seriesIds,
    bulkDeletePlatform,
    onModalClose,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteSelectedSeries')}</ModalHeader>
      <ModalBody>
        <div>
          <FormRow>
            <FormLabel>{translate('AddListExclusion')}</FormLabel>
            <FormInputHelpText
              text={translate('AddListExclusionSeriesHelpText')}
            />
            <FormInput
              type={inputTypes.CHECK}
              name="addImportListExclusion"
              value={addImportListExclusion}
              onChange={onDeleteOptionChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>
              {series.length > 1
                ? translate('DeletePlatformFolders')
                : translate('DeletePlatformFolder')}
            </FormLabel>
            <FormInputHelpText
              text={
                series.length > 1
                  ? translate('DeletePlatformFoldersHelpText')
                  : translate('DeletePlatformFolderHelpText')
              }
            />
            <FormInput
              type={inputTypes.CHECK}
              name="deleteFiles"
              value={deleteFiles}
              kind="danger"
              onChange={onDeleteFilesChange}
            />
          </FormRow>
        </div>

        <div className={styles.message}>
          {deleteFiles
            ? translate('DeletePlatformFolderCountWithFilesConfirmation', {
                count: series.length,
              })
            : translate('DeletePlatformFolderCountConfirmation', {
                count: series.length,
              })}
        </div>

        <PlatformDeleteList
          series={series}
          showFileDetails={deleteFiles}
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

export default DeletePlatformModalContent;
