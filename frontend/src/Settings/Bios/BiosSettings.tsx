import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import Label from 'Components/Label';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import usePlatform from 'Platform/usePlatform';
import settingsStyles from 'Settings/Settings.module.css';
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
  platformTitle?: string;
  description?: string;
  md5Hash?: string;
  expectedMd5?: string;
  sha1Hash?: string;
  expectedSha1?: string;
  isRequired?: boolean;
  status?: BiosVerificationStatus | number;
  isVerified?: boolean;
  resolvedPath?: string;
  downloadUrl?: string;
}

export function getPlatformSlug(item: BiosFile): string {
  return item.platformTitle || item.platformSlug || item.gamePlatform || '';
}

interface BiosDownloadButtonProps {
  platform?: string;
  fileName: string;
  isDownloading: boolean;
  isSpinning: boolean;
  onDownload: (platform: string | undefined, fileName: string) => void;
}

function BiosDownloadButton({
  platform,
  fileName,
  isDownloading,
  isSpinning,
  onDownload,
}: BiosDownloadButtonProps) {
  const handlePress = useCallback(() => {
    onDownload(platform, fileName);
  }, [platform, fileName, onDownload]);

  return (
    <SpinnerButton
      size={sizes.SMALL}
      kind={kinds.DEFAULT}
      isSpinning={isSpinning}
      isDisabled={isDownloading}
      onPress={handlePress}
    >
      Download
    </SpinnerButton>
  );
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
    label: () => translate('Platform'),
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
    name: 'status',
    label: () => translate('VerificationStatus'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: 'Actions',
    isVisible: true,
  },
  {
    name: 'expectedHashes',
    label: () => translate('ExpectedHashes'),
    isVisible: true,
  },
];

const PLATFORM_OPTIONS: SelectInputOption[] = [
  { key: '', value: () => translate('AllPlatforms') },
  { key: '3do', value: '3DO Interactive Multiplayer' },
  { key: 'atari5200', value: 'Atari 5200' },
  { key: 'atari7800', value: 'Atari 7800' },
  { key: 'jaguarcd', value: 'Atari Jaguar CD' },
  { key: 'lynx', value: 'Atari Lynx' },
  { key: 'colecovision', value: 'ColecoVision' },
  { key: 'amiga', value: 'Commodore Amiga / CD32 / CDTV' },
  { key: 'channelf', value: 'Fairchild Channel F' },
  { key: 'vectrex', value: 'GCE Vectrex' },
  { key: 'odyssey2', value: 'Magnavox Odyssey² / Videopac' },
  { key: 'intellivision', value: 'Mattel Intellivision' },
  { key: 'msx', value: 'MSX / MSX2 / MSX2+' },
  { key: 'pc98', value: 'NEC PC-98' },
  { key: 'pcfx', value: 'NEC PC-FX' },
  { key: '64dd', value: 'Nintendo 64DD' },
  { key: 'nds', value: 'Nintendo DS' },
  { key: 'dsi', value: 'Nintendo DSi' },
  { key: 'fds', value: 'Nintendo Famicom Disk System' },
  { key: 'gb', value: 'Nintendo Game Boy' },
  { key: 'gba', value: 'Nintendo Game Boy Advance' },
  { key: 'gbc', value: 'Nintendo Game Boy Color' },
  { key: 'gamecube', value: 'Nintendo GameCube' },
  { key: 'pokemonmini', value: 'Nintendo Pokémon Mini' },
  { key: 'satellaview', value: 'Nintendo Satellaview (BS-X)' },
  { key: 'cdi', value: 'Philips CD-i' },
  { key: 'segacd', value: 'Sega CD / Mega-CD' },
  { key: 'dreamcast', value: 'Sega Dreamcast' },
  { key: 'gamegear', value: 'Sega Game Gear' },
  { key: 'sms', value: 'Sega Master System / Mark III' },
  { key: 'naomi', value: 'Sega NAOMI / Atomiswave' },
  { key: 'saturn', value: 'Sega Saturn' },
  { key: 'x68000', value: 'Sharp X68000' },
  { key: 'neogeo', value: 'SNK Neo Geo AES / MVS' },
  { key: 'neogeocd', value: 'SNK Neo Geo CD' },
  { key: 'ps1', value: 'Sony PlayStation (PS1)' },
  { key: 'ps2', value: 'Sony PlayStation 2 (PS2)' },
  { key: 'psp', value: 'Sony PlayStation Portable (PSP)' },
  { key: 'sgb', value: 'Super Game Boy' },
  { key: 'tgcd', value: 'TurboGrafx-CD / PC Engine CD-ROM²' },
];

const REQUIREMENT_OPTIONS: SelectInputOption[] = [
  { key: '', value: 'All Requirements' },
  { key: 'required', value: () => translate('Required') },
  { key: 'optional', value: () => translate('Optional') },
];

const STATUS_OPTIONS: SelectInputOption[] = [
  { key: '', value: 'All Statuses' },
  { key: 'missing', value: () => translate('Missing') },
  { key: 'present', value: () => translate('Present') },
  { key: 'invalidHash', value: () => translate('InvalidHash') },
];

const ACTION_OPTIONS: SelectInputOption[] = [
  { key: '', value: 'All Actions' },
  { key: 'downloadable', value: 'Needs Download' },
  { key: 'verified', value: 'Verified' },
];

function BiosSettings() {
  const queryClient = useQueryClient();
  const [platformSlug, setPlatformSlug] = useState('');
  const [requirementFilter, setRequirementFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [actionFilter, setActionFilter] = useState('');
  const [downloadingFile, setDownloadingFile] = useState<string | null>(null);

  const { data: libraryPlatforms } = usePlatform();

  const libraryPlatformSlugs = useMemo(() => {
    return new Set(
      (libraryPlatforms ?? []).map((p) => p.titleSlug?.toLowerCase().trim())
    );
  }, [libraryPlatforms]);

  const filteredPlatformOptions = useMemo(() => {
    return PLATFORM_OPTIONS.filter((option) => {
      if (!option.key) {
        return true;
      }

      return libraryPlatformSlugs.has(String(option.key).toLowerCase());
    });
  }, [libraryPlatformSlugs]);

  const queryParams = useMemo(() => {
    return {
      platformSlug: platformSlug || undefined,
    };
  }, [platformSlug]);

  const { data, isFetching, error } = useApiQuery<BiosFile[]>({
    path: '/bios',
    queryParams,
  });

  const libraryItems = useMemo(() => {
    if (!data) {
      return [];
    }

    return data.filter((item) => {
      const slug = (
        item.platformSlug ||
        item.gamePlatform ||
        item.platformTitle ||
        ''
      )
        .toLowerCase()
        .trim();
      return libraryPlatformSlugs.has(slug);
    });
  }, [data, libraryPlatformSlugs]);

  const displayedData = useMemo(() => {
    return libraryItems.filter((item) => {
      if (requirementFilter === 'required' && !item.isRequired) {
        return false;
      }

      if (requirementFilter === 'optional' && item.isRequired) {
        return false;
      }

      const itemStatus = getVerificationStatus(item);
      if (
        statusFilter === 'missing' &&
        itemStatus !== BiosVerificationStatus.Missing
      ) {
        return false;
      }

      if (
        statusFilter === 'present' &&
        itemStatus !== BiosVerificationStatus.Present
      ) {
        return false;
      }

      if (
        statusFilter === 'invalidHash' &&
        itemStatus !== BiosVerificationStatus.InvalidHash
      ) {
        return false;
      }

      if (
        actionFilter === 'downloadable' &&
        itemStatus === BiosVerificationStatus.Present
      ) {
        return false;
      }

      if (
        actionFilter === 'verified' &&
        itemStatus !== BiosVerificationStatus.Present
      ) {
        return false;
      }

      return true;
    });
  }, [libraryItems, requirementFilter, statusFilter, actionFilter]);

  const { mutate: downloadBios, isPending: isDownloading } = useApiMutation<
    BiosFile[],
    { platformSlug?: string; fileName?: string; onlyRequired?: boolean }
  >({
    path: '/bios/download',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setDownloadingFile(null);
        queryClient.invalidateQueries({ queryKey: ['/bios'] });
      },
      onError: () => {
        setDownloadingFile(null);
      },
    },
  });

  const handlePlatformChange = useCallback((change: InputChanged<string>) => {
    setPlatformSlug(change.value);
  }, []);

  const handleRequirementChange = useCallback(
    (change: InputChanged<string>) => {
      setRequirementFilter(change.value);
    },
    []
  );

  const handleStatusChange = useCallback((change: InputChanged<string>) => {
    setStatusFilter(change.value);
  }, []);

  const handleActionChange = useCallback((change: InputChanged<string>) => {
    setActionFilter(change.value);
  }, []);

  const handleDownloadAllMissing = useCallback(() => {
    setDownloadingFile('__all__');
    downloadBios({
      platformSlug: platformSlug || undefined,
      onlyRequired: false,
    });
  }, [downloadBios, platformSlug]);

  const handleDownloadRequiredMissing = useCallback(() => {
    setDownloadingFile('__required__');
    downloadBios({
      platformSlug: platformSlug || undefined,
      onlyRequired: true,
    });
  }, [downloadBios, platformSlug]);

  const handleDownloadSingle = useCallback(
    (gamePlatform?: string, fileName?: string) => {
      setDownloadingFile(fileName ?? null);
      downloadBios({
        platformSlug: gamePlatform,
        fileName,
      });
    },
    [downloadBios]
  );

  const missingCount = useMemo(() => {
    return libraryItems.filter(
      (item) => getVerificationStatus(item) !== BiosVerificationStatus.Present
    ).length;
  }, [libraryItems]);

  const missingRequiredCount = useMemo(() => {
    return libraryItems.filter(
      (item) =>
        item.isRequired &&
        getVerificationStatus(item) !== BiosVerificationStatus.Present
    ).length;
  }, [libraryItems]);

  return (
    <PageContent title={translate('Bios')}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Platforms')}
            title={translate('Bios')}
          />

          <div className={styles.filterContainer}>
            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="platformSlug">
                {translate('Platform')}
              </label>
              <SelectInput<string>
                name="platformSlug"
                value={platformSlug}
                values={filteredPlatformOptions}
                onChange={handlePlatformChange}
              />
            </div>

            <div className={styles.filterGroup}>
              <label
                className={styles.filterLabel}
                htmlFor="requirementFilter"
              >
                {translate('Requirement')}
              </label>
              <SelectInput<string>
                name="requirementFilter"
                value={requirementFilter}
                values={REQUIREMENT_OPTIONS}
                onChange={handleRequirementChange}
              />
            </div>

            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="statusFilter">
                {translate('VerificationStatus')}
              </label>
              <SelectInput<string>
                name="statusFilter"
                value={statusFilter}
                values={STATUS_OPTIONS}
                onChange={handleStatusChange}
              />
            </div>

            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="actionFilter">
                Actions
              </label>
              <SelectInput<string>
                name="actionFilter"
                value={actionFilter}
                values={ACTION_OPTIONS}
                onChange={handleActionChange}
              />
            </div>

            <div className={styles.actionGroup}>
              <SpinnerButton
                kind={kinds.PRIMARY}
                isSpinning={isDownloading && downloadingFile === '__required__'}
                isDisabled={isDownloading || missingRequiredCount === 0}
                onPress={handleDownloadRequiredMissing}
              >
                Download All Required ({missingRequiredCount})
              </SpinnerButton>

              <SpinnerButton
                kind={kinds.DEFAULT}
                isSpinning={isDownloading && downloadingFile === '__all__'}
                isDisabled={isDownloading || missingCount === 0}
                onPress={handleDownloadAllMissing}
              >
                Download All Missing ({missingCount})
              </SpinnerButton>
            </div>

            <span className={styles.storageNote}>
              BIOS files are stored automatically inside each platform&apos;s{' '}
              <code>BIOS/</code> subfolder (e.g.{' '}
              <code>{'{PlatformFolder}/BIOS/'}</code>).
            </span>
          </div>

          {isFetching ? <LoadingIndicator /> : null}

          {error ? (
            <Alert kind={kinds.DANGER}>
              {translate('BiosSettingsLoadError')}
            </Alert>
          ) : null}

          {!isFetching && !error && displayedData.length === 0 ? (
            <Alert kind={kinds.INFO}>
              No platforms in your library currently require BIOS files.
            </Alert>
          ) : null}

          {!isFetching && !error && displayedData.length > 0 ? (
            <Table columns={columns}>
              <TableBody>
                {displayedData.map((item, index) => {
                  const platform = getPlatformSlug(item);
                  const md5 = getMd5Hash(item);
                  const sha1 = getSha1Hash(item);
                  const status = getVerificationStatus(item);
                  const key = `${item.gamePlatform}-${item.fileName}-${index}`;

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
                          {item.resolvedPath ? (
                            <span className={styles.resolvedPath}>
                              {item.resolvedPath}
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
                        <Label kind={statusKind}>{statusText}</Label>
                      </TableRowCell>

                      <TableRowCell>
                        {status === BiosVerificationStatus.Present ? (
                          <Label kind={kinds.SUCCESS}>Verified</Label>
                        ) : (
                          <BiosDownloadButton
                            platform={item.gamePlatform}
                            fileName={item.fileName}
                            isDownloading={isDownloading}
                            isSpinning={
                              isDownloading && downloadingFile === item.fileName
                            }
                            onDownload={handleDownloadSingle}
                          />
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
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default BiosSettings;
