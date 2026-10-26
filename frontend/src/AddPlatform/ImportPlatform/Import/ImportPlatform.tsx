import React, { useEffect, useMemo, useRef } from 'react';
import { useParams } from 'react-router';
import {
  setAddPlatformOption,
  useAddPlatformOption,
} from 'AddPlatform/addPlatformOptionsStore';
import { SelectProvider } from 'App/Select/SelectContext';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { kinds } from 'Helpers/Props';
import useRootFolders, { useRootFolder } from 'RootFolder/useRootFolders';
import { useQualityProfilesData } from 'Settings/Profiles/Quality/useQualityProfiles';
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

  const { path, unmappedFolders } = useMemo(() => {
    const rootFolder = rootFolders.find((r) => r.id === rootFolderId);

    return {
      path: rootFolder?.path ?? '',
      unmappedFolders:
        rootFolder?.unmappedFolders.map((unmappedFolders) => {
          return {
            ...unmappedFolders,
            id: unmappedFolders.name,
          };
        }) ?? [],
    };
  }, [rootFolders, rootFolderId]);

  const qualityProfiles = useQualityProfilesData();

  const defaultQualityProfileId = useAddPlatformOption('qualityProfileId');

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

  useEffect(() => {
    if (
      !defaultQualityProfileId ||
      !qualityProfiles.some((p) => p.id === defaultQualityProfileId)
    ) {
      setAddPlatformOption('qualityProfileId', qualityProfiles[0].id);
    }
  }, [defaultQualityProfileId, qualityProfiles]);

  return (
    <SelectProvider items={items}>
      <PageContent title={translate('ImportPlatform')}>
        <PageContentBody ref={scrollerRef}>
          <div className={styles.content}>
            <PageHeading
              scope={translate('Media')}
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

            {!rootFoldersError &&
            !rootFoldersFetching &&
            rootFoldersFetched &&
            !unmappedFolders.length ? (
              <Alert kind={kinds.INFO}>
                {translate('AllSeriesInRootFolderHaveBeenImported', { path })}
              </Alert>
            ) : null}

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
    </SelectProvider>
  );
}

export default ImportPlatform;
