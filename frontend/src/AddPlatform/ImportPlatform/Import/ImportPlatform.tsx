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
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import useRootFolders, { useRootFolder } from 'RootFolder/useRootFolders';
import translate from 'Utilities/String/translate';
import ImportPlatformFooter from './ImportPlatformFooter';
import ImportPlatformList from './ImportPlatformList';
import { clearImportPlatform } from './importPlatformStore';
import styles from './ImportPlatform.module.css';

function ImportPlatform() {
  const { rootFolderId: rootFolderIdString = '' } = useParams<{
    rootFolderId: string;
  }>();
  const rootFolderId = parseInt(rootFolderIdString);

  const {
    isFetching: rootFoldersFetching,
    isFetched: rootFoldersFetched,
    error: rootFoldersError,
    data: rootFolders,
  } = useRootFolders();

  useRootFolder(rootFolderId, false);

  const { path, unmappedFolders, containsRomFiles } = useMemo(() => {
    const rootFolder = rootFolders.find((r) => r.id === rootFolderId);

    return {
      path: rootFolder?.path ?? '',
      containsRomFiles: rootFolder?.containsRomFiles ?? false,
      unmappedFolders:
        rootFolder?.unmappedFolders.map((unmappedFolders) => {
          return {
            ...unmappedFolders,
            id: unmappedFolders.name,
          };
        }) ?? [],
    };
  }, [rootFolders, rootFolderId]);

  const [isInteractiveImportOpen, setIsInteractiveImportOpen] = useState(false);

  useEffect(() => {
    if (containsRomFiles) {
      setIsInteractiveImportOpen(true);
    }
  }, [containsRomFiles]);

  const handleInteractiveImportClose = useCallback(() => {
    setIsInteractiveImportOpen(false);
  }, []);

  const scrollerRef = useRef<HTMLDivElement>(null);

  const items = useMemo(() => {
    return unmappedFolders.map((unmappedFolder) => {
      return {
        ...unmappedFolder,
        id: unmappedFolder.name,
      };
    });
  }, [unmappedFolders]);

  useEffect(() => {
    return () => {
      clearImportPlatform();
    };
  }, [rootFolderId]);

  return (
    <SelectProvider items={items}>
      <PageContent title={translate('ImportPlatform')}>
        <PageContentBody ref={scrollerRef}>
          <div className={styles.content}>
            <PageHeading
              scope={translate('Platforms')}
              title={translate('ImportPlatform')}
            />

            {rootFoldersFetching && !rootFoldersFetched ? (
              <LoadingIndicator />
            ) : null}

            {!rootFoldersFetching && !!rootFoldersError ? (
              <Alert kind={kinds.DANGER}>
                {translate('RootFoldersLoadError')}
              </Alert>
            ) : null}

            {containsRomFiles ? (
              <div style={{ marginBottom: 16 }}>
                <Alert kind={kinds.INFO}>
                  ROM files detected in {path}. Use Interactive Import to match and import individual ROMs into your library.
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

            {!rootFoldersError &&
            !rootFoldersFetching &&
            rootFoldersFetched &&
            !unmappedFolders.length &&
            !containsRomFiles ? (
              <div>
                <Alert kind={kinds.INFO}>
                  {translate('AllSeriesInRootFolderHaveBeenImported', { path })}
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

            {!rootFoldersError &&
            rootFoldersFetched &&
            !!unmappedFolders.length && (
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
                <Button
                  kind={kinds.DEFAULT}
                  onPress={() => setIsInteractiveImportOpen(true)}
                >
                  <Icon name={icons.INTERACTIVE} />
                  {translate('InteractiveImport')}
                </Button>
              </div>
            )}

            {!rootFoldersError &&
            rootFoldersFetched &&
            !!unmappedFolders.length &&
            scrollerRef.current ? (
              <ImportPlatformList items={items} scrollerRef={scrollerRef} />
            ) : null}
          </div>
        </PageContentBody>

        {!rootFoldersError && rootFoldersFetched && !!unmappedFolders.length ? (
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
