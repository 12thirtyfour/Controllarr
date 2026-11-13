import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Alert from 'Components/Alert';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SectionHeading from 'Components/SectionHeading';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, kinds } from 'Helpers/Props';
import ImportFolders from 'ImportFolder/ImportFolders';
import useImportFolders, {
  useAddImportFolder,
} from 'ImportFolder/useImportFolders';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './ImportPlatformSelectFolder.module.css';

function ImportPlatformSelectFolder() {
  const { isFetching, isFetched, error, data } = useImportFolders();
  const { addImportFolder, isAdding, addError, newImportFolder } =
    useAddImportFolder();
  const navigate = useNavigate();

  const isWindows = useIsWindows();

  const [isAddNewImportFolderModalOpen, setIsAddNewImportFolderModalOpen] =
    useState(false);

  const wasAdding = usePrevious(isAdding);

  const hasImportFolders = data.length > 0;
  const goodFolderExample = isWindows ? 'C:\\staging\\roms' : '/staging/roms';
  const badFolderExample = isWindows
    ? 'C:\\staging\\roms\\Super Nintendo'
    : '/staging/roms/Super Nintendo';

  const handleAddNewImportFolderPress = useCallback(() => {
    setIsAddNewImportFolderModalOpen(true);
  }, []);

  const handleAddImportFolderModalClose = useCallback(() => {
    setIsAddNewImportFolderModalOpen(false);
  }, []);

  const handleNewImportFolderSelect = useCallback(
    ({ value }: InputChanged<string>) => {
      addImportFolder({ path: value });
    },
    [addImportFolder]
  );

  useEffect(() => {
    if (!isAdding && wasAdding && !addError && newImportFolder) {
      navigate(`/add/import/${newImportFolder.id}`);
    }
  }, [isAdding, wasAdding, addError, newImportFolder, navigate]);

  return (
    <PageContent title={translate('ImportPlatform')}>
      <PageContentBody>
        <PageHeading
          scope={translate('Media')}
          title={translate('ImportPlatform')}
        />

        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>
            Failed to load import source folders
          </Alert>
        ) : null}

        {!error && isFetched ? (
          <div>
            <SectionHeading
              title="Import Source Folders"
              description="Select an external source folder to scan and import games into your library. Import source folders are strictly sources and are never used to store your library."
            />

            <details className={styles.tips}>
              <summary className={styles.tipsSummary}>
                <Icon
                  className={styles.tipsChevron}
                  name={icons.EXPAND_INDETERMINATE}
                  size={12}
                />
                Import Source Folder Tips
              </summary>

              <ul className={styles.tipsList}>
                <li>
                  Import source folders are external staging folders (e.g.
                  downloads, flash drives, temporary packs).
                </li>
                <li>
                  <InlineMarkdown
                    data={`Select the folder containing platform folders, e.g. **${goodFolderExample}**, rather than individual platform subfolders like **${badFolderExample}**.`}
                  />
                </li>
                <li>
                  Imported media will be organized and moved into your primary
                  library folder configured in Media Management.
                </li>
              </ul>
            </details>

            {hasImportFolders ? <ImportFolders isSelectable={true} /> : null}

            {!isAdding && addError ? (
              <Alert className={styles.addErrorAlert} kind={kinds.DANGER}>
                Failed to add import source folder
                <ul>
                  {Array.isArray(addError.statusBody) ? (
                    addError.statusBody.map((e, index) => (
                      <li key={index}>{e.errorMessage}</li>
                    ))
                  ) : (
                    <li>{JSON.stringify(addError.statusBody)}</li>
                  )}
                </ul>
              </Alert>
            ) : null}

            <div className={styles.chooseFolderButtonContainer}>
              <Button
                kind={hasImportFolders ? kinds.DEFAULT : kinds.PRIMARY}
                onPress={handleAddNewImportFolderPress}
              >
                <Icon className={styles.importButtonIcon} name={icons.DRIVE} />
                {hasImportFolders
                  ? 'Choose Another Source Folder'
                  : 'Add Source Folder to Import'}
              </Button>
            </div>

            <FileBrowserModal
              isOpen={isAddNewImportFolderModalOpen}
              name="importSourcePath"
              value=""
              onChange={handleNewImportFolderSelect}
              onModalClose={handleAddImportFolderModalClose}
            />
          </div>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default ImportPlatformSelectFolder;
