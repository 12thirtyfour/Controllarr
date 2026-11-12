import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router';
import { SelectProvider } from 'App/Select/SelectContext';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { icons, kinds } from 'Helpers/Props';
import useImportFolders, { useImportFolder } from 'ImportFolder/useImportFolders';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import { useRootFolder } from 'RootFolder/useRootFolders';
import translate from 'Utilities/String/translate';
import ImportPlatformFooter from './ImportPlatformFooter';
import ImportPlatformList from './ImportPlatformList';
import { clearImportPlatform } from './importPlatformStore';
import styles from './ImportPlatform.module.css';

function ImportPlatform() {
  const { rootFolderId: sourceFolderIdString = '' } = useParams<{
    rootFolderId: string;
  }>();
  const sourceFolderId = parseInt(sourceFolderIdString);

  const {
    isFetching: importFoldersFetching,
    isFetched: importFoldersFetched,
    error: importFoldersError,
    data: importFolders,
  } = useImportFolders();

  const {
    isFetching: importFolderFetching,
    isFetched: importFolderFetched,
    error: importFolderError,
    data: detailedImportFolder,
  } = useImportFolder(sourceFolderId, false);

  const {
    data: fallbackRootFolder,
  } = useRootFolder(sourceFolderId, false);

  const isLoading =
    (importFoldersFetching && !importFoldersFetched && !detailedImportFolder) ||
    (importFolderFetching && !importFolderFetched && !detailedImportFolder);
  const isReady = importFoldersFetched || importFolderFetched || !!fallbackRootFolder;
  const loadError = importFolderError ?? importFoldersError;

  const { path, unmappedFolders, containsRomFiles } = useMemo(() => {
    const importFolder =
      detailedImportFolder ??
      importFolders.find((r) => r.id === sourceFolderId) ??
      fallbackRootFolder;

    return {
      path: importFolder?.path ?? '',
      containsRomFiles: importFolder?.containsRomFiles ?? false,
      unmappedFolders:
        importFolder?.unmappedFolders.map((unmappedFolder) => {
          return {
            ...unmappedFolder,
            id: unmappedFolder.path || unmappedFolder.name,
          };
        }) ?? [],
    };
  }, [detailedImportFolder, fallbackRootFolder, importFolders, sourceFolderId]);

  const hasLooseRomsOnly = containsRomFiles && unmappedFolders.length === 0;
  const [isInteractiveImportOpen, setIsInteractiveImportOpen] = useState(false);

  useEffect(() => {
    if (hasLooseRomsOnly) {
      setIsInteractiveImportOpen(true);
    }
  }, [hasLooseRomsOnly]);

  const handleInteractiveImportClose = useCallback(() => {
    setIsInteractiveImportOpen(false);
  }, []);

  const scrollerRef = useRef<HTMLDivElement>(null);

  const items = useMemo(() => {
    return unmappedFolders.map((unmappedFolder) => {
      return {
        ...unmappedFolder,
        id: unmappedFolder.path || unmappedFolder.name,
      };
    });
  }, [unmappedFolders]);

  useEffect(() => {
    return () => {
      clearImportPlatform();
    };
  }, [sourceFolderId]);

  return (
    <SelectProvider items={items}>
      <PageContent title={translate('ImportPlatform')}>
        <PageContentBody ref={scrollerRef}>
          <div className={styles.content}>
            <PageHeading
              scope="Import Source"
              title={path ? path : translate('ImportPlatform')}
            />

            {isLoading ? <LoadingIndicator /> : null}

            {!isLoading && !!loadError ? (
              <Alert kind={kinds.DANGER}>
                Failed to load import source folder
              </Alert>
            ) : null}

            {!loadError && isReady && path ? (
              <div style={{ marginBottom: 16 }}>
                <Alert kind={kinds.INFO}>
                  Import source folder: <strong>{path}</strong>. Content will be organized and moved into your primary library folder.
                </Alert>
              </div>
            ) : null}

            {hasLooseRomsOnly ? (
              <div style={{ marginBottom: 16 }}>
                <Alert kind={kinds.INFO}>
                  Loose ROM files detected in {path}. Use Interactive Import to specify a platform per ROM and import them into your library.
                </Alert>
                <div style={{ marginTop: 8 }}>
                  <Button
                    kind={kinds.PRIMARY}
                    onPress={() => setIsInteractiveImportOpen(true)}
                  >
                    <Icon name={icons.INTERACTIVE} />
                    {translate('InteractiveImport')}
                  </Button>
                </div>
              </div>
            ) : null}

            {!loadError &&
            !isLoading &&
            isReady &&
            !unmappedFolders.length &&
            !hasLooseRomsOnly ? (
              <div>
                <Alert kind={kinds.INFO}>
                  All platforms in this import source folder have been imported.
                </Alert>
                <div style={{ marginTop: 12 }}>
                  <Button
                    kind={kinds.PRIMARY}
                    onPress={() => setIsInteractiveImportOpen(true)}
                  >
                    <Icon name={icons.INTERACTIVE} />
                    {translate('InteractiveImport')}
                  </Button>
                </div>
              </div>
            ) : null}

            {!loadError &&
            isReady &&
            !!unmappedFolders.length &&
            scrollerRef.current ? (
              <ImportPlatformList items={items} scrollerRef={scrollerRef} />
            ) : null}
          </div>
        </PageContentBody>

        {!loadError && isReady && !!unmappedFolders.length ? (
          <ImportPlatformFooter />
        ) : null}
      </PageContent>

      <InteractiveImportModal
        isOpen={isInteractiveImportOpen}
        folder={path}
        modalTitle={translate('InteractiveImport')}
        onModalClose={handleInteractiveImportClose}
      />
    </SelectProvider>
  );
}

export default ImportPlatform;
