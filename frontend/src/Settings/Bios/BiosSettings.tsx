import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import PathInput from 'Components/Form/PathInput';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import Label from 'Components/Label';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './BiosSettings.module.css';

export enum BiosVerificationStatus {
  Missing = 0,
  Present = 1,
  InvalidHash = 2,
}

export interface BiosFile {
  fileName: string;
  platformSlug?: string;
  gamePlatform?: string;
  description?: string;
  md5Hash?: string;
  expectedMd5?: string;
  sha1Hash?: string;
  expectedSha1?: string;
  isRequired?: boolean;
  status?: BiosVerificationStatus | number;
  isVerified?: boolean;
}

export function getPlatformSlug(item: BiosFile): string {
  return item.platformSlug || item.gamePlatform || '';
}

export function getMd5Hash(item: BiosFile): string {
  return item.md5Hash || item.expectedMd5 || '';
}

export function getSha1Hash(item: BiosFile): string {
  return item.sha1Hash || item.expectedSha1 || '';
}

export function getVerificationStatus(item: BiosFile): BiosVerificationStatus {
  if (item.status !== undefined) {
    return item.status as BiosVerificationStatus;
  }

  return item.isVerified
    ? BiosVerificationStatus.Present
    : BiosVerificationStatus.Missing;
}

const columns: Column[] = [
  {
    name: 'platformSlug',
    label: () => translate('PlatformSlug'),
    isVisible: true,
  },
  {
    name: 'fileName',
    label: () => translate('BiosFileName'),
    isVisible: true,
  },
  {
    name: 'isRequired',
    label: () => translate('Requirement'),
    isVisible: true,
  },
  {
    name: 'expectedHashes',
    label: () => translate('ExpectedHashes'),
    isVisible: true,
  },
  {
    name: 'status',
    label: () => translate('VerificationStatus'),
    isVisible: true,
  },
];

const PLATFORM_OPTIONS: SelectInputOption[] = [
  { key: '', value: () => translate('AllPlatforms') },
  { key: 'ps1', value: 'PlayStation 1 (ps1)' },
  { key: 'segacd', value: 'Sega CD (segacd)' },
  { key: 'saturn', value: 'Sega Saturn (saturn)' },
  { key: 'dreamcast', value: 'Dreamcast (dreamcast/dc)' },
  { key: 'gba', value: 'Game Boy Advance (gba)' },
  { key: 'nds', value: 'Nintendo DS (nds)' },
  { key: 'fds', value: 'Famicom Disk System (fds)' },
  { key: 'tgcd', value: 'PC Engine / TurboGrafx-CD (tgcd/pcecd)' },
  { key: 'neogeo', value: 'Neo Geo (neogeo)' },
];

function BiosSettings() {
  const [biosDirectory, setBiosDirectory] = useState('');
  const [platformSlug, setPlatformSlug] = useState('');

  const queryParams = useMemo(() => {
    return {
      platformSlug: platformSlug || undefined,
      biosDirectory: biosDirectory || undefined,
    };
  }, [platformSlug, biosDirectory]);

  const { data, isFetching, error } = useApiQuery<BiosFile[]>({
    path: '/bios',
    queryParams,
  });

  const handleDirectoryChange = useCallback((change: InputChanged<string>) => {
    setBiosDirectory(change.value);
  }, []);

  const handlePlatformChange = useCallback((change: InputChanged<string>) => {
    setPlatformSlug(change.value);
  }, []);

  return (
    <SettingsPage title={translate('BiosSettings')} showSave={false}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('Bios')}
          />

          <div className={styles.filterContainer}>
            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="biosDirectory">
                {translate('BiosDirectory')}
              </label>
              <PathInput
                name="biosDirectory"
                value={biosDirectory}
                placeholder="/path/to/bios"
                includeFiles={false}
                hasFileBrowser={true}
                onChange={handleDirectoryChange}
              />
            </div>

            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="platformSlug">
                {translate('PlatformSlug')}
              </label>
              <SelectInput<string>
                name="platformSlug"
                value={platformSlug}
                values={PLATFORM_OPTIONS}
                onChange={handlePlatformChange}
              />
            </div>
          </div>

          {isFetching ? <LoadingIndicator /> : null}

          {error ? (
            <Alert kind={kinds.DANGER}>
              {translate('BiosSettingsLoadError')}
            </Alert>
          ) : null}

          {!isFetching && !error && data ? (
            <Table columns={columns}>
              <TableBody>
                {data.map((item, index) => {
                  const platform = getPlatformSlug(item);
                  const md5 = getMd5Hash(item);
                  const sha1 = getSha1Hash(item);
                  const status = getVerificationStatus(item);
                  const key = `${platform}-${item.fileName}-${index}`;

                  let statusKind: Kind = kinds.WARNING;
                  let statusText = translate('Missing');

                  if (status === BiosVerificationStatus.Present) {
                    statusKind = kinds.SUCCESS;
                    statusText = translate('Present');
                  } else if (status === BiosVerificationStatus.InvalidHash) {
                    statusKind = kinds.DANGER;
                    statusText = translate('InvalidHash');
                  }

                  return (
                    <TableRow key={key}>
                      <TableRowCell>
                        <span className={styles.platformSlug}>{platform}</span>
                      </TableRowCell>

                      <TableRowCell>
                        <div className={styles.fileInfo}>
                          <span className={styles.fileName}>
                            {item.fileName}
                          </span>
                          {item.description ? (
                            <span className={styles.fileDescription}>
                              {item.description}
                            </span>
                          ) : null}
                        </div>
                      </TableRowCell>

                      <TableRowCell>
                        {item.isRequired ? (
                          <Label kind={kinds.PRIMARY}>
                            {translate('Required')}
                          </Label>
                        ) : (
                          <Label kind={kinds.DEFAULT}>
                            {translate('Optional')}
                          </Label>
                        )}
                      </TableRowCell>

                      <TableRowCell>
                        <div className={styles.hashContainer}>
                          {md5 ? (
                            <div className={styles.hash}>
                              <span className={styles.hashLabel}>
                                {translate('ExpectedMd5')}:
                              </span>
                              {md5}
                            </div>
                          ) : null}
                          {sha1 ? (
                            <div className={styles.hash}>
                              <span className={styles.hashLabel}>
                                {translate('ExpectedSha1')}:
                              </span>
                              {sha1}
                            </div>
                          ) : null}
                        </div>
                      </TableRowCell>

                      <TableRowCell>
                        <Label kind={statusKind}>{statusText}</Label>
                      </TableRowCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default BiosSettings;
