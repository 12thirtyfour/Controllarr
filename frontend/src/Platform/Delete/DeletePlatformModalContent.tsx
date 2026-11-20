import React, { useCallback, useState } from 'react';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import { Statistics } from 'Platform/Platform';
import {
  setSeriesDeleteOptions,
  usePlatformDeleteOptions,
} from 'Platform/platformOptionsStore';
import { useDeletePlatform, useSingleSeries } from 'Platform/usePlatform';
import { CheckInputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './DeletePlatformModalContent.module.css';

export interface DeletePlatformModalContentProps {
  seriesId: number;
  onModalClose: () => void;
}

function DeletePlatformModalContent({
  seriesId,
  onModalClose,
}: DeletePlatformModalContentProps) {
  const {
    title,
    path,
    statistics = {} as Statistics,
  } = useSingleSeries(seriesId)!;

  const { addImportListExclusion } = usePlatformDeleteOptions();
  const { romFileCount = 0, sizeOnDisk = 0 } = statistics;
  const [deleteFiles, setDeleteFiles] = useState(false);

  const { deletePlatform } = useDeletePlatform(seriesId, {
    deleteFiles,
    addImportListExclusion,
  });

  const handleDeleteFilesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setDeleteFiles(value);
    },
    []
  );

  const handleDeletePlatformConfirmed = useCallback(() => {
    deletePlatform();

    onModalClose();
  }, [deletePlatform, onModalClose]);

  const handleDeleteOptionChange = useCallback(
    ({ name, value }: CheckInputChanged) => {
      setSeriesDeleteOptions({ [name]: value });
    },
    []
  );

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('DeletePlatformModalHeader', { title })}
      </ModalHeader>

      <ModalBody>
        <div className={styles.pathContainer}>
          <Icon className={styles.pathIcon} name={icons.FOLDER} />

          {path}
        </div>

        <FormRow>
          <FormLabel>{translate('AddListExclusion')}</FormLabel>

          <FormInputHelpText
            text={translate('AddListExclusionSingleSeriesHelpText')}
          />
          <FormInput
            type={inputTypes.CHECK}
            name="addImportListExclusion"
            value={addImportListExclusion}
            onChange={handleDeleteOptionChange}
          />
        </FormRow>

        <FormRow>
          <FormLabel>
            {romFileCount === 0
              ? translate('DeletePlatformFolder')
              : translate('DeleteGamesFiles', { romFileCount })}
          </FormLabel>

          <FormInputHelpText
            text={
              romFileCount === 0
                ? translate('DeletePlatformFolderHelpText')
                : translate('DeleteGamesFilesHelpText')
            }
          />
          <FormInput
            type={inputTypes.CHECK}
            name="deleteFiles"
            value={deleteFiles}
            kind={kinds.DANGER}
            onChange={handleDeleteFilesChange}
          />
        </FormRow>

        {deleteFiles ? (
          <div className={styles.deleteFilesMessage}>
            <div>
              <InlineMarkdown
                data={translate('DeletePlatformFolderConfirmation', { path })}
                blockClassName={styles.folderPath}
              />
            </div>

            {romFileCount ? (
              <div className={styles.deleteCount}>
                {translate('DeletePlatformFolderGameCount', {
                  romFileCount,
                  size: formatBytes(sizeOnDisk),
                })}
              </div>
            ) : null}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>

        <Button kind={kinds.DANGER} onPress={handleDeletePlatformConfirmed}>
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default DeletePlatformModalContent;
